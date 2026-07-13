using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RedisVL.Caches;
using RedisVL.Filters;
using RedisVL.Indexes;
using RedisVL.Queries;
using RedisVL.Schema;
using RedisVL.Vectorizers;
using StackExchange.Redis;

namespace RedisVL.Workflows;

/// <summary>
/// Stores chat message history in Redis and retrieves it either in recency order or by semantic relevance.
/// Each message's content is embedded and indexed with RediSearch so prior turns can be recalled by vector
/// similarity to a prompt.
/// </summary>
public sealed class SemanticMessageHistory
{
    private readonly IDatabase _database;
    private readonly SearchIndex _index;
    private readonly JsonSerializerOptions _serializerOptions;

    /// <summary>Initializes a new <see cref="SemanticMessageHistory" /> over the given database using the supplied options.</summary>
    /// <param name="database">The Redis database used to store and search messages.</param>
    /// <param name="options">The history configuration, including field names, threshold, and embedding attributes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="database" /> or <paramref name="options" /> is <see langword="null" />.</exception>
    public SemanticMessageHistory(IDatabase database, SemanticMessageHistoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        _database = database;
        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Options = options;
        _index = new SearchIndex(
            database,
            new SearchSchema(
                new IndexDefinition(CreateIndexName(options), CreateMessageKeyPrefix(options), StorageType.Hash),
                [
                    new TagFieldDefinition(options.SessionIdFieldName),
                    new TagFieldDefinition(options.RoleFieldName),
                    new TextFieldDefinition(options.ContentFieldName),
                    new TextFieldDefinition(options.MetadataFieldName),
                    new NumericFieldDefinition(options.TimestampFieldName, sortable: true),
                    new NumericFieldDefinition(options.SequenceFieldName, sortable: true),
                    new VectorFieldDefinition(options.EmbeddingFieldName, options.EmbeddingFieldAttributes)
                ]));
    }

    /// <summary>Gets the options this history was configured with.</summary>
    public SemanticMessageHistoryOptions Options { get; }

    /// <summary>Gets the history name, used to derive the index name and key prefixes.</summary>
    public string Name => Options.Name;

    /// <summary>Gets the optional key namespace that isolates this history's keys and index from others sharing a name.</summary>
    public string? KeyNamespace => Options.KeyNamespace;

    /// <summary>Gets the default maximum distance a message must be within to be considered relevant.</summary>
    public double DistanceThreshold => Options.DistanceThreshold;

    /// <summary>Creates the underlying RediSearch index backing this history.</summary>
    /// <param name="options">Optional index creation options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true" /> if the index was created; otherwise <see langword="false" />.</returns>
    public Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default) =>
        _index.CreateAsync(options, cancellationToken);

    /// <summary>Determines whether the underlying index already exists.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true" /> if the index exists; otherwise <see langword="false" />.</returns>
    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default) =>
        _index.ExistsAsync(cancellationToken);

    /// <summary>Drops the underlying index, optionally deleting the stored message documents.</summary>
    /// <param name="deleteDocuments">When <see langword="true" />, deletes the stored messages as well as the index.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default) =>
        _index.DropAsync(deleteDocuments, cancellationToken);

    /// <summary>Appends a message to a session's history using a precomputed embedding of its content.</summary>
    /// <param name="sessionId">The session the message belongs to.</param>
    /// <param name="role">The role of the message author (for example <c>user</c> or <c>assistant</c>).</param>
    /// <param name="content">The message content.</param>
    /// <param name="embedding">The precomputed embedding of the content.</param>
    /// <param name="metadata">Optional metadata serialized and stored alongside the message.</param>
    /// <param name="timestamp">The message timestamp; defaults to the current UTC time when omitted.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The Redis key under which the message was stored.</returns>
    public async Task<string> AppendAsync(
        string sessionId,
        string role,
        string content,
        float[] embedding,
        object? metadata = null,
        DateTimeOffset? timestamp = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        var normalizedRole = NormalizeRole(role);
        var normalizedContent = NormalizeContent(content);
        var normalizedEmbedding = NormalizeEmbedding(embedding);

        cancellationToken.ThrowIfCancellationRequested();

        var sequence = await _database.StringIncrementAsync(CreateSequenceKey(normalizedSessionId))
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        var recordedAt = timestamp ?? DateTimeOffset.UtcNow;
        var key = CreateMessageKey(normalizedSessionId, sequence);
        var entries = new List<HashEntry>
        {
            new(Options.SessionIdFieldName, normalizedSessionId),
            new(Options.RoleFieldName, normalizedRole),
            new(Options.ContentFieldName, normalizedContent),
            new(Options.TimestampFieldName, recordedAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)),
            new(Options.SequenceFieldName, sequence.ToString(CultureInfo.InvariantCulture)),
            new(Options.EmbeddingFieldName, EmbeddingsCache.EncodeFloat32(normalizedEmbedding))
        };

        var metadataPayload = SerializeMetadata(metadata);
        if (metadataPayload is not null)
        {
            entries.Add(new HashEntry(Options.MetadataFieldName, metadataPayload));
        }

        await _database.HashSetAsync(key, entries.ToArray()).WaitAsync(cancellationToken).ConfigureAwait(false);
        return key!;
    }

    /// <summary>Appends a message to a session's history, vectorizing its content with <paramref name="vectorizer" />.</summary>
    /// <param name="sessionId">The session the message belongs to.</param>
    /// <param name="role">The role of the message author (for example <c>user</c> or <c>assistant</c>).</param>
    /// <param name="content">The message content to store and vectorize.</param>
    /// <param name="vectorizer">The vectorizer used to embed the content.</param>
    /// <param name="metadata">Optional metadata serialized and stored alongside the message.</param>
    /// <param name="timestamp">The message timestamp; defaults to the current UTC time when omitted.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The Redis key under which the message was stored.</returns>
    public async Task<string> AppendAsync(
        string sessionId,
        string role,
        string content,
        ITextVectorizer vectorizer,
        object? metadata = null,
        DateTimeOffset? timestamp = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectorizer);

        var normalizedContent = NormalizeContent(content);
        var embedding = await vectorizer.VectorizeAsync(normalizedContent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await AppendAsync(sessionId, role, normalizedContent, embedding, metadata, timestamp, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns the most recent messages for a session, ordered newest-first.</summary>
    /// <param name="sessionId">The session to read history for.</param>
    /// <param name="limit">The maximum number of messages to return.</param>
    /// <param name="role">When set, restricts results to messages with this role.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The most recent messages, ordered by sequence then timestamp descending.</returns>
    public async Task<IReadOnlyList<MessageHistoryMessage>> GetRecentAsync(
        string sessionId,
        int limit = 10,
        string? role = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        var normalizedRole = string.IsNullOrWhiteSpace(role) ? null : NormalizeRole(role);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var filter = BuildSessionFilter(normalizedSessionId, normalizedRole);
        var result = await ExecuteSearchAsync(BuildRecentArguments(filter, limit), cancellationToken).ConfigureAwait(false);
        return SearchResultsParser.Parse(result)
            .Documents
            .Select(MapMessage)
            .OrderByDescending(static message => message.Sequence)
            .ThenByDescending(static message => message.Timestamp)
            .ToArray();
    }

    /// <summary>
    /// Returns the messages in a session most semantically relevant to a precomputed embedding, ordered
    /// nearest-first and limited to those within the effective distance threshold.
    /// </summary>
    /// <param name="sessionId">The session to search within.</param>
    /// <param name="embedding">The precomputed query embedding to compare messages against.</param>
    /// <param name="limit">The maximum number of matches to return.</param>
    /// <param name="role">When set, restricts results to messages with this role.</param>
    /// <param name="distanceThreshold">The maximum distance a message may be to qualify, or <see langword="null" /> to use the configured default.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The relevant messages with their distances, ordered nearest-first.</returns>
    public async Task<IReadOnlyList<SemanticMessageHistoryMatch>> GetRelevantAsync(
        string sessionId,
        float[] embedding,
        int limit = 5,
        string? role = null,
        double? distanceThreshold = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        var normalizedRole = string.IsNullOrWhiteSpace(role) ? null : NormalizeRole(role);
        var normalizedEmbedding = NormalizeEmbedding(embedding);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var threshold = NormalizeDistanceThreshold(distanceThreshold);
        var filter = BuildSessionFilter(normalizedSessionId, normalizedRole);
        var results = await _index.SearchAsync<SemanticMessageHistoryDocument>(
            VectorRangeQuery.FromFloat32(
                Options.EmbeddingFieldName,
                normalizedEmbedding,
                threshold,
                filter,
                [
                    Options.SessionIdFieldName,
                    Options.RoleFieldName,
                    Options.ContentFieldName,
                    Options.MetadataFieldName,
                    Options.TimestampFieldName,
                    "id"
                ],
                scoreAlias: "distance",
                limit: limit),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return results.Documents
            .Select(MapRelevantMessage)
            .OrderBy(static match => match.Distance)
            .ThenByDescending(static match => match.Message.Sequence)
            .ToArray();
    }

    /// <summary>
    /// Returns the messages in a session most semantically relevant to a prompt, vectorizing the prompt with
    /// <paramref name="vectorizer" /> and ordering results nearest-first within the effective distance threshold.
    /// </summary>
    /// <param name="sessionId">The session to search within.</param>
    /// <param name="prompt">The prompt to embed and compare messages against.</param>
    /// <param name="vectorizer">The vectorizer used to embed the prompt.</param>
    /// <param name="limit">The maximum number of matches to return.</param>
    /// <param name="role">When set, restricts results to messages with this role.</param>
    /// <param name="distanceThreshold">The maximum distance a message may be to qualify, or <see langword="null" /> to use the configured default.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The relevant messages with their distances, ordered nearest-first.</returns>
    public async Task<IReadOnlyList<SemanticMessageHistoryMatch>> GetRelevantAsync(
        string sessionId,
        string prompt,
        ITextVectorizer vectorizer,
        int limit = 5,
        string? role = null,
        double? distanceThreshold = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectorizer);

        var normalizedPrompt = NormalizeContent(prompt);
        var embedding = await vectorizer.VectorizeAsync(normalizedPrompt, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return await GetRelevantAsync(sessionId, embedding, limit, role, distanceThreshold, cancellationToken).ConfigureAwait(false);
    }

    internal RedisKey CreateMessageKey(string sessionId, long sequence)
    {
        var sessionHash = HashSessionId(sessionId);
        return $"{CreateMessageKeyPrefix(Options)}{sessionHash}:{sequence.ToString("D20", CultureInfo.InvariantCulture)}";
    }

    internal RedisKey CreateSequenceKey(string sessionId)
    {
        var sessionHash = HashSessionId(sessionId);
        return $"{CreateSequenceKeyPrefix(Options)}{sessionHash}";
    }

    private async Task<RedisResult> ExecuteSearchAsync(object[] arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _database.ExecuteAsync("FT.SEARCH", arguments).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private object[] BuildRecentArguments(FilterExpression filter, int limit)
    {
        return
        [
            Options.NameForIndex(),
            filter.ToQueryString(),
            "SORTBY",
            Options.SequenceFieldName,
            "DESC",
            "RETURN",
            "5",
            Options.SessionIdFieldName,
            Options.RoleFieldName,
            Options.ContentFieldName,
            Options.MetadataFieldName,
            Options.TimestampFieldName,
            "LIMIT",
            "0",
            limit.ToString(CultureInfo.InvariantCulture),
            "DIALECT",
            "2"
        ];
    }

    private FilterExpression BuildSessionFilter(string sessionId, string? role)
    {
        var sessionFilter = Filter.Tag(Options.SessionIdFieldName).Eq(sessionId);
        return role is null
            ? sessionFilter
            : sessionFilter & Filter.Tag(Options.RoleFieldName).Eq(role);
    }

    private MessageHistoryMessage MapMessage(SearchDocument document)
    {
        var sessionId = GetRequiredValue(document, Options.SessionIdFieldName);
        var role = GetRequiredValue(document, Options.RoleFieldName);
        var content = GetRequiredValue(document, Options.ContentFieldName);
        var timestamp = ParseTimestamp(GetRequiredValue(document, Options.TimestampFieldName));
        var sequence = ParseSequence(document.Id);
        var metadata = document.TryGetValue(Options.MetadataFieldName, out var metadataValue) && !metadataValue.IsNull
            ? metadataValue.ToString()
            : null;

        return new MessageHistoryMessage(sessionId, role, content, timestamp, metadata, sequence, document.Id);
    }

    private SemanticMessageHistoryMatch MapRelevantMessage(SemanticMessageHistoryDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.Id))
        {
            throw new InvalidOperationException("Semantic message history result is missing the document id.");
        }

        var message = new MessageHistoryMessage(
            document.SessionId,
            document.Role,
            document.Content,
            ParseTimestamp(document.Timestamp),
            document.Metadata,
            ParseSequence(document.Id),
            document.Id);

        return new SemanticMessageHistoryMatch(message, document.Distance);
    }

    private static string GetRequiredValue(SearchDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out var value) || value.IsNullOrEmpty)
        {
            throw new InvalidOperationException($"Semantic message history search result is missing required field '{fieldName}'.");
        }

        return value.ToString()!;
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds))
        {
            throw new InvalidOperationException("Semantic message history timestamp must be stored as Unix time milliseconds.");
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
    }

    private long ParseSequence(string documentId)
    {
        if (!documentId.StartsWith(CreateMessageKeyPrefix(Options), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Semantic message history document id does not match the configured key prefix.");
        }

        var lastSeparatorIndex = documentId.LastIndexOf(':');
        if (lastSeparatorIndex < 0)
        {
            throw new InvalidOperationException("Semantic message history document id does not contain a sequence suffix.");
        }

        var suffix = documentId[(lastSeparatorIndex + 1)..];
        if (!long.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence))
        {
            throw new InvalidOperationException("Semantic message history document id sequence suffix must be numeric.");
        }

        return sequence;
    }

    private float[] NormalizeEmbedding(float[] embedding)
    {
        ArgumentNullException.ThrowIfNull(embedding);

        if (embedding.Length != Options.EmbeddingFieldAttributes.Dimensions)
        {
            throw new ArgumentException(
                $"Semantic message embedding must contain exactly {Options.EmbeddingFieldAttributes.Dimensions} values.",
                nameof(embedding));
        }

        return embedding.ToArray();
    }

    private double NormalizeDistanceThreshold(double? distanceThreshold)
    {
        var threshold = distanceThreshold ?? Options.DistanceThreshold;
        if (threshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceThreshold), threshold, "Semantic message history distance threshold must be greater than zero.");
        }

        return threshold;
    }

    private string? SerializeMetadata(object? metadata) =>
        metadata is null ? null : JsonSerializer.Serialize(metadata, _serializerOptions);

    private static string CreateIndexName(SemanticMessageHistoryOptions options) =>
        string.IsNullOrEmpty(options.KeyNamespace)
            ? $"semantic-message-history:{options.Name}"
            : $"semantic-message-history:{options.Name}:{options.KeyNamespace}";

    private static string CreateMessageKeyPrefix(SemanticMessageHistoryOptions options) =>
        string.IsNullOrEmpty(options.KeyNamespace)
            ? $"semantic-message-history:{options.Name}:msg:"
            : $"semantic-message-history:{options.Name}:{options.KeyNamespace}:msg:";

    private static string CreateSequenceKeyPrefix(SemanticMessageHistoryOptions options) =>
        string.IsNullOrEmpty(options.KeyNamespace)
            ? $"semantic-message-history:{options.Name}:seq:"
            : $"semantic-message-history:{options.Name}:{options.KeyNamespace}:seq:";

    private static string HashSessionId(string sessionId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId))).ToLowerInvariant();

    private static string NormalizeSessionId(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return sessionId.Trim();
    }

    private static string NormalizeRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return role.Trim();
    }

    private static string NormalizeContent(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        return content.Trim();
    }

    private sealed record SemanticMessageHistoryDocument(
        string SessionId,
        string Role,
        string Content,
        string Timestamp,
        string? Metadata,
        string Id,
        double Distance);
}

internal static class SemanticMessageHistoryOptionsExtensions
{
    public static string NameForIndex(this SemanticMessageHistoryOptions options) =>
        string.IsNullOrEmpty(options.KeyNamespace)
            ? $"semantic-message-history:{options.Name}"
            : $"semantic-message-history:{options.Name}:{options.KeyNamespace}";
}
