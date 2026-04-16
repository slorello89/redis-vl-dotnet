using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RedisVl.Caches;
using RedisVl.Filters;
using RedisVl.Indexes;
using RedisVl.Queries;
using RedisVl.Schema;
using RedisVl.Vectorizers;
using StackExchange.Redis;

namespace RedisVl.Workflows;

public sealed class SemanticMessageHistory
{
    private readonly IDatabase _database;
    private readonly SearchIndex _index;
    private readonly JsonSerializerOptions _serializerOptions;

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

    public SemanticMessageHistoryOptions Options { get; }

    public string Name => Options.Name;

    public string? KeyNamespace => Options.KeyNamespace;

    public double DistanceThreshold => Options.DistanceThreshold;

    public bool Create(CreateIndexOptions? options = null) =>
        CreateAsync(options).GetAwaiter().GetResult();

    public Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default) =>
        _index.CreateAsync(options, cancellationToken);

    public bool Exists() =>
        ExistsAsync().GetAwaiter().GetResult();

    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default) =>
        _index.ExistsAsync(cancellationToken);

    public void Drop(bool deleteDocuments = false) =>
        DropAsync(deleteDocuments).GetAwaiter().GetResult();

    public Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default) =>
        _index.DropAsync(deleteDocuments, cancellationToken);

    public string Append(
        string sessionId,
        string role,
        string content,
        float[] embedding,
        object? metadata = null,
        DateTimeOffset? timestamp = null) =>
        AppendAsync(sessionId, role, content, embedding, metadata, timestamp).GetAwaiter().GetResult();

    public string Append(
        string sessionId,
        string role,
        string content,
        ITextVectorizer vectorizer,
        object? metadata = null,
        DateTimeOffset? timestamp = null) =>
        AppendAsync(sessionId, role, content, vectorizer, metadata, timestamp).GetAwaiter().GetResult();

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

    public IReadOnlyList<MessageHistoryMessage> GetRecent(
        string sessionId,
        int limit = 10,
        string? role = null) =>
        GetRecentAsync(sessionId, limit, role).GetAwaiter().GetResult();

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

    public IReadOnlyList<SemanticMessageHistoryMatch> GetRelevant(
        string sessionId,
        float[] embedding,
        int limit = 5,
        string? role = null,
        double? distanceThreshold = null) =>
        GetRelevantAsync(sessionId, embedding, limit, role, distanceThreshold).GetAwaiter().GetResult();

    public IReadOnlyList<SemanticMessageHistoryMatch> GetRelevant(
        string sessionId,
        string prompt,
        ITextVectorizer vectorizer,
        int limit = 5,
        string? role = null,
        double? distanceThreshold = null) =>
        GetRelevantAsync(sessionId, prompt, vectorizer, limit, role, distanceThreshold).GetAwaiter().GetResult();

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
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId)));

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
