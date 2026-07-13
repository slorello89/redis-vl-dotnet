using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RedisVL.Filters;
using RedisVL.Indexes;
using RedisVL.Schema;
using StackExchange.Redis;

namespace RedisVL.Workflows;

/// <summary>
/// Stores chat message history in Redis and retrieves it in recency order. Messages are kept as hashes and
/// indexed with RediSearch by session, role, timestamp, and a monotonic per-session sequence number.
/// </summary>
public sealed class MessageHistory : IMessageHistory
{
    private readonly IDatabase _database;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ISearchIndex _index;

    /// <summary>Initializes a new <see cref="MessageHistory" /> over the given database using the supplied options.</summary>
    /// <param name="database">The Redis database used to store and search messages.</param>
    /// <param name="options">The history configuration, including field names and naming.</param>
    /// <exception cref="ArgumentNullException"><paramref name="database" /> or <paramref name="options" /> is <see langword="null" />.</exception>
    public MessageHistory(IDatabase database, MessageHistoryOptions options)
        : this(database, options, index: null)
    {
    }

    // Lets tests supply a substitute ISearchIndex instead of the one this history would otherwise build
    // from its options. Not public: an externally supplied index could carry a schema that does not
    // match the history's field expectations.
    internal MessageHistory(IDatabase database, MessageHistoryOptions options, ISearchIndex? index)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        _database = database;
        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Options = options;
        _index = index ?? new SearchIndex(
            database,
            new SearchSchema(
                new IndexDefinition(CreateIndexName(options), CreateMessageKeyPrefix(options), StorageType.Hash),
                [
                    new TagFieldDefinition(options.SessionIdFieldName),
                    new TagFieldDefinition(options.RoleFieldName),
                    new TextFieldDefinition(options.ContentFieldName),
                    new TextFieldDefinition(options.MetadataFieldName),
                    new NumericFieldDefinition(options.TimestampFieldName, sortable: true),
                    new NumericFieldDefinition(options.SequenceFieldName, sortable: true)
                ]));
    }

    /// <summary>Gets the options this history was configured with.</summary>
    public MessageHistoryOptions Options { get; }

    /// <summary>Gets the history name, used to derive the index name and key prefixes.</summary>
    public string Name => Options.Name;

    /// <summary>Gets the optional key namespace that isolates this history's keys and index from others sharing a name.</summary>
    public string? KeyNamespace => Options.KeyNamespace;

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

    /// <summary>Appends a message to a session's history.</summary>
    /// <param name="sessionId">The session the message belongs to.</param>
    /// <param name="role">The role of the message author (for example <c>user</c> or <c>assistant</c>).</param>
    /// <param name="content">The message content.</param>
    /// <param name="metadata">Optional metadata serialized and stored alongside the message.</param>
    /// <param name="timestamp">The message timestamp; defaults to the current UTC time when omitted.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The Redis key under which the message was stored.</returns>
    public async Task<string> AppendAsync(
        string sessionId,
        string role,
        string content,
        object? metadata = null,
        DateTimeOffset? timestamp = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        var normalizedRole = NormalizeRole(role);
        var normalizedContent = NormalizeContent(content);

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
            new(Options.SequenceFieldName, sequence.ToString(CultureInfo.InvariantCulture))
        };

        var metadataPayload = SerializeMetadata(metadata);
        if (metadataPayload is not null)
        {
            entries.Add(new HashEntry(Options.MetadataFieldName, metadataPayload));
        }

        await _database.HashSetAsync(key, entries.ToArray()).WaitAsync(cancellationToken).ConfigureAwait(false);
        return key!;
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

        var filter = normalizedRole is null
            ? Filter.Tag(Options.SessionIdFieldName).Eq(normalizedSessionId)
            : Filter.Tag(Options.SessionIdFieldName).Eq(normalizedSessionId) & Filter.Tag(Options.RoleFieldName).Eq(normalizedRole);
        var result = await ExecuteSearchAsync(BuildRecentArguments(filter, limit), cancellationToken).ConfigureAwait(false);
        var messages = SearchResultsParser.Parse(result)
            .Documents
            .Select(MapMessage)
            .OrderByDescending(static message => message.Sequence)
            .ThenByDescending(static message => message.Timestamp)
            .ToArray();

        return messages;
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

    private MessageHistoryMessage MapMessage(Queries.SearchDocument document)
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

    private static string GetRequiredValue(Queries.SearchDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out var value) || value.IsNullOrEmpty)
        {
            throw new InvalidOperationException($"Message history search result is missing required field '{fieldName}'.");
        }

        return value.ToString()!;
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds))
        {
            throw new InvalidOperationException("Message history timestamp must be stored as Unix time milliseconds.");
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
    }

    private long ParseSequence(string documentId)
    {
        if (!documentId.StartsWith(CreateMessageKeyPrefix(Options), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Message history document id does not match the configured key prefix.");
        }

        var lastSeparatorIndex = documentId.LastIndexOf(':');
        if (lastSeparatorIndex < 0)
        {
            throw new InvalidOperationException("Message history document id does not contain a sequence suffix.");
        }

        var suffix = documentId[(lastSeparatorIndex + 1)..];
        if (!long.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence))
        {
            throw new InvalidOperationException("Message history document id sequence suffix must be numeric.");
        }

        return sequence;
    }

    private string? SerializeMetadata(object? metadata) =>
        metadata is null ? null : JsonSerializer.Serialize(metadata, _serializerOptions);

    private static string CreateIndexName(MessageHistoryOptions options) =>
        string.IsNullOrEmpty(options.KeyNamespace)
            ? $"message-history:{options.Name}"
            : $"message-history:{options.Name}:{options.KeyNamespace}";

    private static string CreateMessageKeyPrefix(MessageHistoryOptions options) =>
        string.IsNullOrEmpty(options.KeyNamespace)
            ? $"message-history:{options.Name}:msg:"
            : $"message-history:{options.Name}:{options.KeyNamespace}:msg:";

    private static string CreateSequenceKeyPrefix(MessageHistoryOptions options) =>
        string.IsNullOrEmpty(options.KeyNamespace)
            ? $"message-history:{options.Name}:seq:"
            : $"message-history:{options.Name}:{options.KeyNamespace}:seq:";

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
}

internal static class MessageHistoryOptionsExtensions
{
    public static string NameForIndex(this MessageHistoryOptions options) =>
        string.IsNullOrEmpty(options.KeyNamespace)
            ? $"message-history:{options.Name}"
            : $"message-history:{options.Name}:{options.KeyNamespace}";
}
