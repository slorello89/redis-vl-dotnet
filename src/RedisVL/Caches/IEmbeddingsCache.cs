namespace RedisVL.Caches;

/// <summary>
/// Abstraction over an embeddings cache, mirroring the public surface of <see cref="EmbeddingsCache" />.
/// Depend on this interface where the cache needs to be substituted in unit tests.
/// </summary>
public interface IEmbeddingsCache
{
    EmbeddingsCacheOptions Options { get; }

    string Name { get; }

    string? KeyNamespace { get; }

    TimeSpan? TimeToLive { get; }

    Task<bool> StoreAsync(string input, float[] embedding, CancellationToken cancellationToken = default);

    Task<bool> StoreAsync(
        string input,
        float[] embedding,
        object? metadata,
        CancellationToken cancellationToken = default);

    Task<bool> StoreAsync(
        string input,
        float[] embedding,
        object? metadata,
        TimeSpan? timeToLive,
        CancellationToken cancellationToken = default);

    Task<bool> StoreAsync(
        string input,
        string modelName,
        float[] embedding,
        CancellationToken cancellationToken = default);

    Task<bool> StoreAsync(
        string input,
        string modelName,
        float[] embedding,
        object? metadata,
        CancellationToken cancellationToken = default);

    Task<bool> StoreAsync(
        string input,
        string modelName,
        float[] embedding,
        object? metadata,
        TimeSpan? timeToLive,
        CancellationToken cancellationToken = default);

    Task<EmbeddingsCacheEntry> SetAsync(string input, float[] embedding, CancellationToken cancellationToken = default);

    Task<EmbeddingsCacheEntry> SetAsync(
        string input,
        float[] embedding,
        object? metadata,
        CancellationToken cancellationToken = default);

    Task<EmbeddingsCacheEntry> SetAsync(
        string input,
        float[] embedding,
        object? metadata,
        TimeSpan? timeToLive,
        CancellationToken cancellationToken = default);

    Task<EmbeddingsCacheEntry> SetAsync(
        string input,
        string modelName,
        float[] embedding,
        CancellationToken cancellationToken = default);

    Task<EmbeddingsCacheEntry> SetAsync(
        string input,
        string modelName,
        float[] embedding,
        object? metadata,
        CancellationToken cancellationToken = default);

    Task<EmbeddingsCacheEntry> SetAsync(
        string input,
        string modelName,
        float[] embedding,
        object? metadata,
        TimeSpan? timeToLive,
        CancellationToken cancellationToken = default);

    Task<bool> StoreManyAsync(
        IReadOnlyList<EmbeddingsCacheWriteRequest> entries,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmbeddingsCacheEntry>> SetManyAsync(
        IReadOnlyList<EmbeddingsCacheWriteRequest> entries,
        CancellationToken cancellationToken = default);

    Task<EmbeddingsCacheEntry?> GetAsync(string input, CancellationToken cancellationToken = default);

    Task<EmbeddingsCacheEntry?> GetAsync(
        string input,
        string modelName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmbeddingsCacheEntry?>> GetManyAsync(
        IReadOnlyList<EmbeddingsCacheLookup> lookups,
        CancellationToken cancellationToken = default);

    Task<EmbeddingsCacheEntry?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmbeddingsCacheEntry?>> GetManyByKeyAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken = default);

    Task<EmbeddingsCacheEntry?> LookupAsync(string input, CancellationToken cancellationToken = default);

    Task<EmbeddingsCacheEntry?> LookupAsync(
        string input,
        string modelName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmbeddingsCacheEntry?>> LookupManyAsync(
        IReadOnlyList<EmbeddingsCacheLookup> lookups,
        CancellationToken cancellationToken = default);

    Task<float[]?> LookupEmbeddingAsync(string input, CancellationToken cancellationToken = default);

    Task<float[]?> LookupEmbeddingAsync(
        string input,
        string modelName,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string input, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string input,
        string modelName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<bool>> ExistsManyAsync(
        IReadOnlyList<EmbeddingsCacheLookup> lookups,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<bool>> ExistsManyByKeyAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string input, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string input,
        string modelName,
        CancellationToken cancellationToken = default);

    Task<long> DeleteManyAsync(
        IReadOnlyList<EmbeddingsCacheLookup> lookups,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<long> DeleteManyByKeyAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken = default);
}
