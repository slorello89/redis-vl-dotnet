using RedisVL.Indexes;

namespace RedisVL.Workflows;

/// <summary>
/// Abstraction over a chat message history, mirroring the public surface of <see cref="MessageHistory" />.
/// Depend on this interface where the history needs to be substituted in unit tests.
/// </summary>
public interface IMessageHistory
{
    MessageHistoryOptions Options { get; }

    string Name { get; }

    string? KeyNamespace { get; }

    Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

    Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default);

    Task<string> AppendAsync(
        string sessionId,
        string role,
        string content,
        object? metadata = null,
        DateTimeOffset? timestamp = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessageHistoryMessage>> GetRecentAsync(
        string sessionId,
        int limit = 10,
        string? role = null,
        CancellationToken cancellationToken = default);
}
