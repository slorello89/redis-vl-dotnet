using RedisVL.Indexes;
using RedisVL.Vectorizers;

namespace RedisVL.Workflows;

/// <summary>
/// Abstraction over a semantic chat message history, mirroring the public surface of
/// <see cref="SemanticMessageHistory" />. Depend on this interface where the history needs to be
/// substituted in unit tests.
/// </summary>
public interface ISemanticMessageHistory
{
    /// <summary>Gets the configuration this history was created with.</summary>
    SemanticMessageHistoryOptions Options { get; }

    /// <summary>Gets the history name (from <see cref="Options" />).</summary>
    string Name { get; }

    /// <summary>Gets the optional key namespace (from <see cref="Options" />), or <see langword="null" /> when unset.</summary>
    string? KeyNamespace { get; }

    /// <summary>Gets the default maximum vector distance for a message to be considered relevant (from <see cref="Options" />).</summary>
    double DistanceThreshold { get; }

    /// <summary>Creates the history's underlying search index.</summary>
    Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Determines whether the history's underlying search index exists.</summary>
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>Drops the history's underlying search index, optionally deleting the stored messages.</summary>
    Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default);

    /// <summary>Appends a message with a precomputed content embedding and returns the key it was stored under.</summary>
    Task<string> AppendAsync(
        string sessionId,
        string role,
        string content,
        float[] embedding,
        object? metadata = null,
        DateTimeOffset? timestamp = null,
        CancellationToken cancellationToken = default);

    /// <summary>Appends a message, vectorizing its content with the supplied vectorizer, and returns the key it was stored under.</summary>
    Task<string> AppendAsync(
        string sessionId,
        string role,
        string content,
        ITextVectorizer vectorizer,
        object? metadata = null,
        DateTimeOffset? timestamp = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent messages for a session, ordered newest-first.</summary>
    Task<IReadOnlyList<MessageHistoryMessage>> GetRecentAsync(
        string sessionId,
        int limit = 10,
        string? role = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the messages most relevant to a precomputed query embedding, ordered nearest-first.</summary>
    Task<IReadOnlyList<SemanticMessageHistoryMatch>> GetRelevantAsync(
        string sessionId,
        float[] embedding,
        int limit = 5,
        string? role = null,
        double? distanceThreshold = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the messages most relevant to a prompt, vectorizing it with the supplied vectorizer, ordered nearest-first.</summary>
    Task<IReadOnlyList<SemanticMessageHistoryMatch>> GetRelevantAsync(
        string sessionId,
        string prompt,
        ITextVectorizer vectorizer,
        int limit = 5,
        string? role = null,
        double? distanceThreshold = null,
        CancellationToken cancellationToken = default);
}
