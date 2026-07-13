using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RedisVL.Internal;
using StackExchange.Redis;

namespace RedisVL.Caches;

public sealed class EmbeddingsCache : IEmbeddingsCache
{
    private const string InputFieldName = "input";
    private const string ModelNameFieldName = "model_name";
    private const string EmbeddingFieldName = "embedding";
    private const string MetadataFieldName = "metadata";
    private const char KeyHashSeparator = '\n';

    private readonly IDatabase _database;
    private readonly JsonSerializerOptions _serializerOptions;

    public EmbeddingsCache(IDatabase database, EmbeddingsCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        _database = database;
        _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Options = options;
    }

    public EmbeddingsCacheOptions Options { get; }

    public string Name => Options.Name;

    public string? KeyNamespace => Options.KeyNamespace;

    public TimeSpan? TimeToLive => Options.TimeToLive;

    public async Task<bool> StoreAsync(string input, float[] embedding, CancellationToken cancellationToken = default)
    {
        await SetAsync(input, embedding, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> StoreAsync(
        string input,
        float[] embedding,
        object? metadata,
        CancellationToken cancellationToken = default)
    {
        await SetAsync(input, embedding, metadata, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> StoreAsync(
        string input,
        float[] embedding,
        object? metadata,
        TimeSpan? timeToLive,
        CancellationToken cancellationToken = default)
    {
        await SetAsync(input, embedding, metadata, timeToLive, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> StoreAsync(
        string input,
        string modelName,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        await SetAsync(input, modelName, embedding, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> StoreAsync(
        string input,
        string modelName,
        float[] embedding,
        object? metadata,
        CancellationToken cancellationToken = default)
    {
        await SetAsync(input, modelName, embedding, metadata, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> StoreAsync(
        string input,
        string modelName,
        float[] embedding,
        object? metadata,
        TimeSpan? timeToLive,
        CancellationToken cancellationToken = default)
    {
        await SetAsync(input, modelName, embedding, metadata, timeToLive, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public Task<EmbeddingsCacheEntry> SetAsync(string input, float[] embedding, CancellationToken cancellationToken = default) =>
        SetAsyncCore(input, embedding, modelName: null, metadata: null, timeToLive: null, cancellationToken);

    public Task<EmbeddingsCacheEntry> SetAsync(
        string input,
        float[] embedding,
        object? metadata,
        CancellationToken cancellationToken = default) =>
        SetAsyncCore(input, embedding, modelName: null, metadata, timeToLive: null, cancellationToken);

    public Task<EmbeddingsCacheEntry> SetAsync(
        string input,
        float[] embedding,
        object? metadata,
        TimeSpan? timeToLive,
        CancellationToken cancellationToken = default) =>
        SetAsyncCore(input, embedding, modelName: null, metadata, timeToLive, cancellationToken);

    public Task<EmbeddingsCacheEntry> SetAsync(
        string input,
        string modelName,
        float[] embedding,
        CancellationToken cancellationToken = default) =>
        SetAsyncCore(input, embedding, NormalizeModelName(modelName), metadata: null, timeToLive: null, cancellationToken);

    public Task<EmbeddingsCacheEntry> SetAsync(
        string input,
        string modelName,
        float[] embedding,
        object? metadata,
        CancellationToken cancellationToken = default) =>
        SetAsyncCore(input, embedding, NormalizeModelName(modelName), metadata, timeToLive: null, cancellationToken);

    public Task<EmbeddingsCacheEntry> SetAsync(
        string input,
        string modelName,
        float[] embedding,
        object? metadata,
        TimeSpan? timeToLive,
        CancellationToken cancellationToken = default) =>
        SetAsyncCore(input, embedding, NormalizeModelName(modelName), metadata, timeToLive, cancellationToken);

    public async Task<bool> StoreManyAsync(
        IReadOnlyList<EmbeddingsCacheWriteRequest> entries,
        CancellationToken cancellationToken = default)
    {
        await SetManyAsync(entries, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Stores multiple embedding entries, returning results aligned to input order.</summary>
    /// <remarks>
    /// The writes are pipelined (dispatched concurrently) rather than awaited one at a time. The batch
    /// is not transactional: if a write fails, entries dispatched alongside it may already have been
    /// stored and are not rolled back.
    /// </remarks>
    public async Task<IReadOnlyList<EmbeddingsCacheEntry>> SetManyAsync(
        IReadOnlyList<EmbeddingsCacheWriteRequest> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return [];
        }

        return await RedisBatch.RunAsync(
            entries,
            (entry, _, token) => SetAsyncCore(
                entry.Input,
                entry.Embedding,
                entry.ModelName is null ? null : NormalizeModelName(entry.ModelName),
                entry.Metadata,
                entry.TimeToLive,
                token),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<EmbeddingsCacheEntry?> GetAsync(string input, CancellationToken cancellationToken = default) =>
        LookupAsyncCore(input, modelName: null, cancellationToken);

    public Task<EmbeddingsCacheEntry?> GetAsync(
        string input,
        string modelName,
        CancellationToken cancellationToken = default) =>
        LookupAsyncCore(input, NormalizeModelName(modelName), cancellationToken);

    public async Task<IReadOnlyList<EmbeddingsCacheEntry?>> GetManyAsync(
        IReadOnlyList<EmbeddingsCacheLookup> lookups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lookups);

        if (lookups.Count == 0)
        {
            return [];
        }

        return await RedisBatch.RunAsync(
            lookups,
            (lookup, _, token) => lookup.ModelName is null
                ? GetAsync(lookup.Input, token)
                : GetAsync(lookup.Input, lookup.ModelName, token),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<EmbeddingsCacheEntry?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        GetByKeyAsyncCore(NormalizeKey(key), cancellationToken);

    public async Task<IReadOnlyList<EmbeddingsCacheEntry?>> GetManyByKeyAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        if (keys.Count == 0)
        {
            return [];
        }

        return await RedisBatch.RunAsync(
            keys,
            (key, _, token) => GetByKeyAsync(key, token),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<EmbeddingsCacheEntry?> LookupAsync(string input, CancellationToken cancellationToken = default) =>
        GetAsync(input, cancellationToken);

    public Task<EmbeddingsCacheEntry?> LookupAsync(
        string input,
        string modelName,
        CancellationToken cancellationToken = default) =>
        GetAsync(input, modelName, cancellationToken);

    public Task<IReadOnlyList<EmbeddingsCacheEntry?>> LookupManyAsync(
        IReadOnlyList<EmbeddingsCacheLookup> lookups,
        CancellationToken cancellationToken = default) =>
        GetManyAsync(lookups, cancellationToken);

    public async Task<float[]?> LookupEmbeddingAsync(string input, CancellationToken cancellationToken = default)
    {
        return (await LookupAsync(input, cancellationToken).ConfigureAwait(false))?.Embedding;
    }

    public async Task<float[]?> LookupEmbeddingAsync(
        string input,
        string modelName,
        CancellationToken cancellationToken = default)
    {
        return (await LookupAsync(input, modelName, cancellationToken).ConfigureAwait(false))?.Embedding;
    }

    public Task<bool> ExistsAsync(string input, CancellationToken cancellationToken = default) =>
        ExistsAsyncCore(CreateKey(NormalizeInput(input)), cancellationToken);

    public Task<bool> ExistsAsync(
        string input,
        string modelName,
        CancellationToken cancellationToken = default) =>
        ExistsAsyncCore(CreateKey(NormalizeInput(input), NormalizeModelName(modelName)), cancellationToken);

    public async Task<IReadOnlyList<bool>> ExistsManyAsync(
        IReadOnlyList<EmbeddingsCacheLookup> lookups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lookups);

        if (lookups.Count == 0)
        {
            return [];
        }

        return await RedisBatch.RunAsync(
            lookups,
            (lookup, _, token) => lookup.ModelName is null
                ? ExistsAsync(lookup.Input, token)
                : ExistsAsync(lookup.Input, lookup.ModelName, token),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> ExistsByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        ExistsAsyncCore(NormalizeKey(key), cancellationToken);

    public async Task<IReadOnlyList<bool>> ExistsManyByKeyAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        if (keys.Count == 0)
        {
            return [];
        }

        return await RedisBatch.RunAsync(
            keys,
            (key, _, token) => ExistsByKeyAsync(key, token),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> DeleteAsync(string input, CancellationToken cancellationToken = default) =>
        DeleteAsyncCore(CreateKey(NormalizeInput(input)), cancellationToken);

    public Task<bool> DeleteAsync(
        string input,
        string modelName,
        CancellationToken cancellationToken = default) =>
        DeleteAsyncCore(CreateKey(NormalizeInput(input), NormalizeModelName(modelName)), cancellationToken);

    public async Task<long> DeleteManyAsync(
        IReadOnlyList<EmbeddingsCacheLookup> lookups,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lookups);

        if (lookups.Count == 0)
        {
            return 0;
        }

        var deletions = await RedisBatch.RunAsync(
            lookups,
            (lookup, _, token) => lookup.ModelName is null
                ? DeleteAsync(lookup.Input, token)
                : DeleteAsync(lookup.Input, lookup.ModelName, token),
            cancellationToken).ConfigureAwait(false);

        return deletions.Count(static deleted => deleted);
    }

    public Task<bool> DeleteByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        DeleteAsyncCore(NormalizeKey(key), cancellationToken);

    public async Task<long> DeleteManyByKeyAsync(
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        if (keys.Count == 0)
        {
            return 0;
        }

        // Delete one key per command rather than a single multi-key DEL: on a cluster a batch
        // spanning multiple hash slots would raise CROSSSLOT. The commands are pipelined over the
        // multiplexer, so this is still a single round trip, matching SearchIndex.ClearAsync.
        var deletions = await RedisBatch.RunAsync(
            keys,
            (key, _, token) => DeleteByKeyAsync(key, token),
            cancellationToken).ConfigureAwait(false);

        return deletions.Count(static deleted => deleted);
    }

    internal RedisKey CreateKey(string input) => CreateKey(input, modelName: null);

    internal RedisKey CreateKey(string input, string? modelName)
    {
        var identity = string.IsNullOrEmpty(modelName)
            ? input
            : string.Concat(input, KeyHashSeparator, modelName);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        return string.IsNullOrEmpty(KeyNamespace)
            ? $"embeddings:{Name}:{hash}"
            : $"embeddings:{Name}:{KeyNamespace}:{hash}";
    }

    internal static byte[] EncodeFloat32(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    internal static float[] DecodeFloat32(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Length % sizeof(float) != 0)
        {
            throw new InvalidOperationException("Cached embedding payload length must align to 32-bit floating point values.");
        }

        var values = new float[payload.Length / sizeof(float)];
        Buffer.BlockCopy(payload, 0, values, 0, payload.Length);
        return values;
    }

    private async Task<EmbeddingsCacheEntry> SetAsyncCore(
        string input,
        float[] embedding,
        string? modelName,
        object? metadata,
        TimeSpan? timeToLive,
        CancellationToken cancellationToken)
    {
        var normalizedInput = NormalizeInput(input);
        ArgumentNullException.ThrowIfNull(embedding);
        ValidateTimeToLive(timeToLive);

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedModelName = modelName ?? string.Empty;
        var entries = new List<HashEntry>
        {
            new(InputFieldName, normalizedInput),
            new(ModelNameFieldName, normalizedModelName),
            new(EmbeddingFieldName, EncodeFloat32(embedding))
        };

        // Metadata is the only optional field, so clear it when this write carries none to avoid an
        // HSET-merge leaving a previous entry's metadata attached to the new embedding.
        var metadataPayload = SerializeMetadata(metadata);
        RedisValue[] fieldsToClear = metadataPayload is null ? [MetadataFieldName] : [];
        if (metadataPayload is not null)
        {
            entries.Add(new HashEntry(MetadataFieldName, metadataPayload));
        }

        var key = CreateKey(normalizedInput, modelName);
        await WriteEntriesAsync(key, entries, fieldsToClear, timeToLive ?? TimeToLive, cancellationToken).ConfigureAwait(false);

        return new EmbeddingsCacheEntry(
            normalizedInput,
            embedding,
            modelName,
            metadataPayload,
            key);
    }

    private async Task WriteEntriesAsync(
        RedisKey key,
        IReadOnlyList<HashEntry> entries,
        IReadOnlyList<RedisValue> fieldsToClear,
        TimeSpan? timeToLive,
        CancellationToken cancellationToken)
    {
        // A plain HSET suffices only when there is nothing else to do; otherwise group the write,
        // the stale-field cleanup, and the TTL into a single MULTI/EXEC so an entry can never be
        // left with a stale optional field or without its configured TTL (for example when the
        // connection drops or the token is cancelled between the HSET and the EXPIRE).
        if (fieldsToClear.Count == 0 && !timeToLive.HasValue)
        {
            await _database.HashSetAsync(key, entries.ToArray()).WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var transaction = _database.CreateTransaction();
        _ = transaction.HashSetAsync(key, entries.ToArray());
        if (fieldsToClear.Count > 0)
        {
            _ = transaction.HashDeleteAsync(key, fieldsToClear.ToArray());
        }

        if (timeToLive.HasValue)
        {
            _ = transaction.KeyExpireAsync(key, timeToLive);
        }

        await transaction.ExecuteAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<EmbeddingsCacheEntry?> LookupAsyncCore(
        string input,
        string? modelName,
        CancellationToken cancellationToken)
    {
        var normalizedInput = NormalizeInput(input);
        var entry = await GetByKeyAsyncCore(CreateKey(normalizedInput, modelName), cancellationToken).ConfigureAwait(false);

        if (entry is null ||
            !string.Equals(entry.Input, normalizedInput, StringComparison.Ordinal) ||
            !string.Equals(entry.ModelName, modelName, StringComparison.Ordinal))
        {
            return null;
        }

        return entry;
    }

    private async Task<EmbeddingsCacheEntry?> GetByKeyAsyncCore(
        RedisKey key,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entries = await _database
            .HashGetAllAsync(key)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        return TryCreateEntry(key, entries);
    }

    private async Task<bool> ExistsAsyncCore(
        RedisKey key,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await _database
            .KeyExistsAsync(key)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> DeleteAsyncCore(
        RedisKey key,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await _database
            .KeyDeleteAsync(key)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static EmbeddingsCacheEntry? TryCreateEntry(RedisKey key, HashEntry[] entries)
    {
        if (entries.Length == 0)
        {
            return null;
        }

        string? cachedInput = null;
        string? cachedModelName = null;
        byte[]? payload = null;
        string? metadata = null;

        foreach (var entry in entries)
        {
            if (entry.Name == InputFieldName)
            {
                cachedInput = entry.Value;
                continue;
            }

            if (entry.Name == ModelNameFieldName)
            {
                cachedModelName = entry.Value.IsNull ? null : entry.Value.ToString();
                continue;
            }

            if (entry.Name == EmbeddingFieldName && !entry.Value.IsNull)
            {
                payload = (byte[]?)entry.Value;
                continue;
            }

            if (entry.Name == MetadataFieldName && !entry.Value.IsNull)
            {
                metadata = entry.Value.ToString();
            }
        }

        if (string.IsNullOrWhiteSpace(cachedInput) || payload is null)
        {
            return null;
        }

        var normalizedCachedModelName = string.IsNullOrEmpty(cachedModelName) ? null : cachedModelName;
        return new EmbeddingsCacheEntry(
            cachedInput,
            DecodeFloat32(payload),
            normalizedCachedModelName,
            metadata,
            key);
    }

    private static string NormalizeInput(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        return input;
    }

    private static string NormalizeModelName(string modelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        return modelName;
    }

    private static RedisKey NormalizeKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return key;
    }

    private static void ValidateTimeToLive(TimeSpan? timeToLive)
    {
        if (timeToLive.HasValue && timeToLive.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), "Cache TTL must be positive when provided.");
        }
    }

    private string? SerializeMetadata(object? metadata) =>
        metadata is null ? null : JsonSerializer.Serialize(metadata, _serializerOptions);
}
