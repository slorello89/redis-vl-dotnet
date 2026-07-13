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
    /// <summary>Gets the configuration this cache was created with.</summary>
    SemanticCacheOptions Options { get; }

    /// <summary>Gets the cache name (from <see cref="Options" />).</summary>
    string Name { get; }

    /// <summary>Gets the optional key namespace (from <see cref="Options" />), or <see langword="null" /> when unset.</summary>
    string? KeyNamespace { get; }

    /// <summary>Gets the default entry expiry (from <see cref="Options" />), or <see langword="null" /> for no expiry.</summary>
    TimeSpan? TimeToLive { get; }

    /// <summary>Gets the maximum vector distance for an entry to count as a match (from <see cref="Options" />).</summary>
    double DistanceThreshold { get; }

    /// <summary>Gets the number of tracked cache lookups that returned a hit.</summary>
    long HitCount { get; }

    /// <summary>Gets the number of tracked cache lookups that returned a miss.</summary>
    long MissCount { get; }

    /// <summary>Gets the fraction of tracked lookups that returned a hit, or <c>0</c> when none have been tracked.</summary>
    double HitRate { get; }

    /// <summary>Resets the tracked hit and miss counters to zero.</summary>
    void ResetStatistics();

    /// <summary>Creates the cache's underlying search index.</summary>
    Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Determines whether the cache's underlying search index exists.</summary>
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>Drops the cache's underlying search index, optionally deleting the cached entries.</summary>
    Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default);

    /// <summary>Looks up the single nearest cached entry within the distance threshold using a precomputed embedding.</summary>
    Task<SemanticCacheHit?> CheckAsync(
        string prompt,
        float[] embedding,
        FilterExpression? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>Looks up the single nearest cached entry, vectorizing the prompt with the supplied vectorizer.</summary>
    Task<SemanticCacheHit?> CheckAsync(
        string prompt,
        ITextVectorizer vectorizer,
        FilterExpression? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns up to <paramref name="topK" /> cached entries within the distance threshold, nearest-first, using a precomputed embedding.</summary>
    Task<IReadOnlyList<SemanticCacheHit>> CheckTopKAsync(
        string prompt,
        float[] embedding,
        int topK,
        FilterExpression? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns up to <paramref name="topK" /> cached entries, vectorizing the prompt with the supplied vectorizer.</summary>
    Task<IReadOnlyList<SemanticCacheHit>> CheckTopKAsync(
        string prompt,
        ITextVectorizer vectorizer,
        int topK,
        FilterExpression? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a batch of lookups using precomputed embeddings; the result is aligned to input order with <see langword="null" /> for misses.</summary>
    Task<IReadOnlyList<SemanticCacheHit?>> CheckManyAsync(
        IEnumerable<SemanticCacheCheckRequest> requests,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a batch of lookups, vectorizing all prompts in a single batch via the supplied vectorizer.</summary>
    Task<IReadOnlyList<SemanticCacheHit?>> CheckManyAsync(
        IEnumerable<SemanticCacheCheckRequest> requests,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default);

    /// <summary>Stores a prompt/response pair using a precomputed embedding and returns its key.</summary>
    Task<string> StoreAsync(
        string prompt,
        string response,
        float[] embedding,
        object? metadata = null,
        IReadOnlyDictionary<string, object?>? filterValues = null,
        CancellationToken cancellationToken = default);

    /// <summary>Stores a prompt/response pair, vectorizing the prompt with the supplied vectorizer, and returns its key.</summary>
    Task<string> StoreAsync(
        string prompt,
        string response,
        ITextVectorizer vectorizer,
        object? metadata = null,
        IReadOnlyDictionary<string, object?>? filterValues = null,
        CancellationToken cancellationToken = default);

    /// <summary>Stores multiple prompt/response pairs using precomputed embeddings; returned keys are aligned to input order.</summary>
    Task<IReadOnlyList<string>> StoreManyAsync(
        IEnumerable<SemanticCacheStoreRequest> requests,
        CancellationToken cancellationToken = default);

    /// <summary>Stores multiple prompt/response pairs, vectorizing all prompts in a single batch via the supplied vectorizer.</summary>
    Task<IReadOnlyList<string>> StoreManyAsync(
        IEnumerable<SemanticCacheStoreRequest> requests,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default);

    /// <summary>Updates the response and/or metadata of an existing cached entry identified by its key.</summary>
    Task<bool> UpdateAsync(
        string key,
        string? response = null,
        object? metadata = null,
        CancellationToken cancellationToken = default);
}
