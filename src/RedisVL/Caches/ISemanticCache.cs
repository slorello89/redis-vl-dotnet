using RedisVL.Filters;
using RedisVL.Indexes;
using RedisVL.Vectorizers;

namespace RedisVL.Caches;

/// <summary>
/// Abstraction over a semantic (vector) response cache, mirroring the public surface of
/// <see cref="SemanticCache" />. Depend on this interface where the cache needs to be substituted in
/// unit tests.
/// </summary>
public interface ISemanticCache
{
    SemanticCacheOptions Options { get; }

    string Name { get; }

    string? KeyNamespace { get; }

    TimeSpan? TimeToLive { get; }

    double DistanceThreshold { get; }

    long HitCount { get; }

    long MissCount { get; }

    double HitRate { get; }

    void ResetStatistics();

    Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

    Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default);

    Task<SemanticCacheHit?> CheckAsync(
        string prompt,
        float[] embedding,
        FilterExpression? filter = null,
        CancellationToken cancellationToken = default);

    Task<SemanticCacheHit?> CheckAsync(
        string prompt,
        ITextVectorizer vectorizer,
        FilterExpression? filter = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SemanticCacheHit>> CheckTopKAsync(
        string prompt,
        float[] embedding,
        int topK,
        FilterExpression? filter = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SemanticCacheHit>> CheckTopKAsync(
        string prompt,
        ITextVectorizer vectorizer,
        int topK,
        FilterExpression? filter = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SemanticCacheHit?>> CheckManyAsync(
        IEnumerable<SemanticCacheCheckRequest> requests,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SemanticCacheHit?>> CheckManyAsync(
        IEnumerable<SemanticCacheCheckRequest> requests,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default);

    Task<string> StoreAsync(
        string prompt,
        string response,
        float[] embedding,
        object? metadata = null,
        IReadOnlyDictionary<string, object?>? filterValues = null,
        CancellationToken cancellationToken = default);

    Task<string> StoreAsync(
        string prompt,
        string response,
        ITextVectorizer vectorizer,
        object? metadata = null,
        IReadOnlyDictionary<string, object?>? filterValues = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> StoreManyAsync(
        IEnumerable<SemanticCacheStoreRequest> requests,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> StoreManyAsync(
        IEnumerable<SemanticCacheStoreRequest> requests,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        string key,
        string? response = null,
        object? metadata = null,
        CancellationToken cancellationToken = default);
}
