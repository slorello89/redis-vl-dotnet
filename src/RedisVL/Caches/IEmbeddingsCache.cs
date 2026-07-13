namespace RedisVL.Caches;

/// <summary>
/// Abstraction over an embeddings cache, mirroring the public surface of <see cref="EmbeddingsCache" />.
/// Depend on this interface where the cache needs to be substituted in unit tests.
/// </summary>
public interface IEmbeddingsCache
{
    /// <summary>Gets the configuration this cache was created with.</summary>
    EmbeddingsCacheOptions Options { get; }

    /// <summary>Gets the cache name (from <see cref="Options" />).</summary>
    string Name { get; }

    /// <summary>Gets the optional key namespace (from <see cref="Options" />), or <see langword="null" /> when unset.</summary>
    string? KeyNamespace { get; }

    /// <summary>Gets the default entry expiry (from <see cref="Options" />), or <see langword="null" /> for no expiry.</summary>
    TimeSpan? TimeToLive { get; }

    /// <summary>Stores an embedding for the input using the cache's default TTL.</summary>
    Task<bool> StoreAsync(string input, float[] embedding, CancellationToken cancellationToken = default);

    /// <summary>Stores an embedding for the input with metadata, using the cache's default TTL.</summary>
    Task<bool> StoreAsync(
        string input,
        float[] embedding,
        object? metadata,
        CancellationToken cancellationToken = default);

    /// <summary>Stores an embedding for the input with metadata and an explicit expiry.</summary>
    Task<bool> StoreAsync(
        string input,
        float[] embedding,
        object? metadata,
        TimeSpan? timeToLive,
        CancellationToken cancellationToken = default);

    /// <summary>Stores an embedding for the input scoped to a model name, using the cache's default TTL.</summary>
    Task<bool> StoreAsync(
        string input,
        string modelName,
        float[] embedding,
        CancellationToken cancellationToken = default);

    /// <summary>Stores an embedding for the input scoped to a model name with metadata, using the cache's default TTL.</summary>
    Task<bool> StoreAsync(
        string input,
        string modelName,
        float[] embedding,
        object? metadata,
        CancellationToken cancellationToken = default);

    /// <summary>Stores an embedding for the input scoped to a model name with metadata and an explicit expiry.</summary>
    Task<bool> StoreAsync(
        string input,
        string modelName,
        float[] embedding,
        object? metadata,
        TimeSpan? timeToLive,
        CancellationToken cancellationToken = default);

    /// <summary>Stores an embedding and returns the resulting cache entry, using the cache's default TTL.</summary>
    Task<EmbeddingsCacheEntry> SetAsync(string input, float[] embedding, CancellationToken cancellationToken = default);

    /// <summary>Stores an embedding with metadata and returns the resulting cache entry, using the cache's default TTL.</summary>
    Task<EmbeddingsCacheEntry> SetAsync(
        string input,
        float[] embedding,
        object? metadata,
        CancellationToken cancellationToken = default);

    /// <summary>Stores an embedding with metadata and an explicit expiry, returning the resulting cache entry.</summary>
    Task<EmbeddingsCacheEntry> SetAsync(
        string input,
        float[] embedding,
        object? metadata,
        TimeSpan? timeToLive,
        CancellationToken cancellationToken = default);

    /// <summary>Stores an embedding scoped to a model name and returns the resulting cache entry, using the cache's default TTL.</summary>
    Task<EmbeddingsCacheEntry> SetAsync(
        string input,
        string modelName,
        float[] embedding,
        CancellationToken cancellationToken = default);

    /// <summary>Stores an embedding scoped to a model name with metadata, returning the resulting cache entry.</summary>
    Task<EmbeddingsCacheEntry> SetAsync(
        string input,
        string modelName,
        float[] embedding,
        object? metadata,
        CancellationToken cancellationToken = default);

    /// <summary>Stores an embedding scoped to a model name with metadata and an explicit expiry, returning the resulting cache entry.</summary>
    Task<EmbeddingsCacheEntry> SetAsync(
        string input,
        string modelName,
        float[] embedding,
        object? metadata,
        TimeSpan? timeToLive,
        CancellationToken cancellationToken = default);

    /// <summary>Stores multiple embedding entries; returns <see langword="true" /> once all have been written.</summary>
    Task<bool> StoreManyAsync(
        IReadOnlyList<EmbeddingsCacheWriteRequest> entries,
        CancellationToken cancellationToken = default);

    /// <summary>Stores multiple embedding entries and returns the resulting entries, aligned to input order.</summary>
    Task<IReadOnlyList<EmbeddingsCacheEntry>> SetManyAsync(
        IReadOnlyList<EmbeddingsCacheWriteRequest> entries,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the cached entry for the input, or <see langword="null" /> when absent.</summary>
    Task<EmbeddingsCacheEntry?> GetAsync(string input, CancellationToken cancellationToken = default);

    /// <summary>Gets the cached entry for the input scoped to a model name, or <see langword="null" /> when absent.</summary>
    Task<EmbeddingsCacheEntry?> GetAsync(
        string input,
        string modelName,
        CancellationToken cancellationToken = default);

    /// <summary>Gets multiple cached entries; the result is aligned to input order with <see langword="null" /> for misses.</summary>
    Task<IReadOnlyList<EmbeddingsCacheEntry?>> GetManyAsync(
        IReadOnlyList<EmbeddingsCacheLookup> lookups,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a cached entry by its full Redis key, or <see langword="null" /> when absent.</summary>
    Task<EmbeddingsCacheEntry?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Gets multiple cached entries by key; the result is aligned to input order with <see langword="null" /> for misses.</summary>
    Task<IReadOnlyList<EmbeddingsCacheEntry?>> GetManyByKeyAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the cached entry for the input, or <see langword="null" /> when absent. Alias for <see cref="GetAsync(string, CancellationToken)" />.</summary>
    Task<EmbeddingsCacheEntry?> LookupAsync(string input, CancellationToken cancellationToken = default);

    /// <summary>Gets the cached entry for the input scoped to a model name, or <see langword="null" /> when absent. Alias for <see cref="GetAsync(string, string, CancellationToken)" />.</summary>
    Task<EmbeddingsCacheEntry?> LookupAsync(
        string input,
        string modelName,
        CancellationToken cancellationToken = default);

    /// <summary>Gets multiple cached entries. Alias for <see cref="GetManyAsync" />.</summary>
    Task<IReadOnlyList<EmbeddingsCacheEntry?>> LookupManyAsync(
        IReadOnlyList<EmbeddingsCacheLookup> lookups,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the cached embedding vector for the input, or <see langword="null" /> when absent.</summary>
    Task<float[]?> LookupEmbeddingAsync(string input, CancellationToken cancellationToken = default);

    /// <summary>Gets the cached embedding vector for the input scoped to a model name, or <see langword="null" /> when absent.</summary>
    Task<float[]?> LookupEmbeddingAsync(
        string input,
        string modelName,
        CancellationToken cancellationToken = default);

    /// <summary>Determines whether a cached entry exists for the input.</summary>
    Task<bool> ExistsAsync(string input, CancellationToken cancellationToken = default);

    /// <summary>Determines whether a cached entry exists for the input scoped to a model name.</summary>
    Task<bool> ExistsAsync(
        string input,
        string modelName,
        CancellationToken cancellationToken = default);

    /// <summary>Determines existence for multiple inputs; the result is aligned to input order.</summary>
    Task<IReadOnlyList<bool>> ExistsManyAsync(
        IReadOnlyList<EmbeddingsCacheLookup> lookups,
        CancellationToken cancellationToken = default);

    /// <summary>Determines whether a cached entry exists for the given full Redis key.</summary>
    Task<bool> ExistsByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Determines existence for multiple keys; the result is aligned to input order.</summary>
    Task<IReadOnlyList<bool>> ExistsManyByKeyAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the cached entry for the input.</summary>
    Task<bool> DeleteAsync(string input, CancellationToken cancellationToken = default);

    /// <summary>Deletes the cached entry for the input scoped to a model name.</summary>
    Task<bool> DeleteAsync(
        string input,
        string modelName,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes multiple cached entries and returns the number actually removed.</summary>
    Task<long> DeleteManyAsync(
        IReadOnlyList<EmbeddingsCacheLookup> lookups,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a cached entry by its full Redis key.</summary>
    Task<bool> DeleteByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Deletes multiple cached entries by key and returns the number actually removed.</summary>
    Task<long> DeleteManyByKeyAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken = default);
}
