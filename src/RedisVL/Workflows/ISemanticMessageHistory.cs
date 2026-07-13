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
    SemanticMessageHistoryOptions Options { get; }

    string Name { get; }

    string? KeyNamespace { get; }

    double DistanceThreshold { get; }

    Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

    Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default);

    Task<string> AppendAsync(
        string sessionId,
        string role,
        string content,
        float[] embedding,
        object? metadata = null,
        DateTimeOffset? timestamp = null,
        CancellationToken cancellationToken = default);

    Task<string> AppendAsync(
        string sessionId,
        string role,
        string content,
        ITextVectorizer vectorizer,
        object? metadata = null,
        DateTimeOffset? timestamp = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessageHistoryMessage>> GetRecentAsync(
        string sessionId,
        int limit = 10,
        string? role = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SemanticMessageHistoryMatch>> GetRelevantAsync(
        string sessionId,
        float[] embedding,
        int limit = 5,
        string? role = null,
        double? distanceThreshold = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SemanticMessageHistoryMatch>> GetRelevantAsync(
        string sessionId,
        string prompt,
        ITextVectorizer vectorizer,
        int limit = 5,
        string? role = null,
        double? distanceThreshold = null,
        CancellationToken cancellationToken = default);
}
