using RedisVL.Indexes;

namespace RedisVL.Workflows;

/// <summary>
/// Abstraction over a chat message history, mirroring the public surface of <see cref="MessageHistory" />.
/// Depend on this interface where the history needs to be substituted in unit tests.
/// </summary>
public interface IMessageHistory
{
    /// <summary>Gets the configuration this history was created with.</summary>
    MessageHistoryOptions Options { get; }

    /// <summary>Gets the history name (from <see cref="Options" />).</summary>
    string Name { get; }

    /// <summary>Gets the optional key namespace (from <see cref="Options" />), or <see langword="null" /> when unset.</summary>
    string? KeyNamespace { get; }

    /// <summary>Creates the history's underlying search index.</summary>
    Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Determines whether the history's underlying search index exists.</summary>
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>Drops the history's underlying search index, optionally deleting the stored messages.</summary>
    Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default);

    /// <summary>Appends a message to a session's history and returns the key it was stored under.</summary>
    Task<string> AppendAsync(
        string sessionId,
        string role,
        string content,
        object? metadata = null,
        DateTimeOffset? timestamp = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent messages for a session, ordered newest-first.</summary>
    Task<IReadOnlyList<MessageHistoryMessage>> GetRecentAsync(
        string sessionId,
        int limit = 10,
        string? role = null,
        CancellationToken cancellationToken = default);
}
