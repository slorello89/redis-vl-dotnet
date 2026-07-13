using Microsoft.Extensions.AI;
using RedisVL.Indexes;
using RedisVL.Workflows;
using StackExchange.Redis;

namespace RedisVL.Connectors.VectorData.ChatMemory;

/// <summary>
/// Bridges Microsoft.Extensions.AI <see cref="ChatMessage"/> conversation history to the RedisVL
/// <see cref="MessageHistory"/> workflow, keyed by session id. This is the .NET analog of the
/// LangChain4J <c>RedisVLChatMemoryStore</c>: a place to persist and retrieve chat turns.
/// </summary>
public sealed class RedisVLChatMessageStore
{
    private readonly MessageHistory _history;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisVLChatMessageStore"/> class backed by a new
    /// <see cref="MessageHistory"/> built from the given database and options.
    /// </summary>
    /// <param name="database">The StackExchange.Redis database backing the message history.</param>
    /// <param name="options">The message-history configuration.</param>
    public RedisVLChatMessageStore(IDatabase database, MessageHistoryOptions options)
        : this(new MessageHistory(database, options))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisVLChatMessageStore"/> class wrapping an
    /// existing <see cref="MessageHistory"/> workflow.
    /// </summary>
    /// <param name="history">The message-history workflow to wrap.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="history"/> is <c>null</c>.</exception>
    public RedisVLChatMessageStore(MessageHistory history)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
    }

    /// <summary>The underlying message-history workflow.</summary>
    public MessageHistory History => _history;

    /// <summary>Creates the backing index if it does not already exist.</summary>
    public Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default) =>
        _history.CreateAsync(options ?? new CreateIndexOptions(skipIfExists: true), cancellationToken);

    /// <summary>Appends a single chat message to the session's history.</summary>
    public Task AddMessageAsync(string sessionId, ChatMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return AddMessagesAsync(sessionId, [message], cancellationToken);
    }

    /// <summary>Appends chat messages to the session's history, preserving order.</summary>
    public async Task AddMessagesAsync(
        string sessionId,
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(messages);

        foreach (var message in messages)
        {
            var content = message.Text;
            if (string.IsNullOrWhiteSpace(content))
            {
                // MessageHistory stores textual turns; skip content-free messages (e.g. pure tool calls).
                continue;
            }

            object? metadata = string.IsNullOrWhiteSpace(message.AuthorName)
                ? null
                : new { authorName = message.AuthorName };

            await _history.AppendAsync(
                sessionId,
                message.Role.Value,
                content,
                metadata,
                message.CreatedAt,
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Returns the most recent <paramref name="limit"/> messages for the session in chronological
    /// (oldest-first) order.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        string sessionId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var recent = await _history.GetRecentAsync(sessionId, limit, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // GetRecentAsync returns newest-first; reverse to chronological order for chat consumption.
        return recent
            .Reverse()
            .Select(static message => new ChatMessage(new ChatRole(message.Role), message.Content)
            {
                CreatedAt = message.Timestamp,
            })
            .ToArray();
    }
}
