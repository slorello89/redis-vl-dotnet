using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;

namespace RedisVL.Caches;

public sealed class EmbeddingsCache
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

    public bool Store(string input, float[] embedding) =>
        StoreAsync(input, embedding).GetAwaiter().GetResult();

    public bool Store(string input, float[] embedding, TimeSpan? timeToLive) =>
        StoreAsync(input, embedding, metadata: null, timeToLive).GetAwaiter().GetResult();

    public bool Store(string input, float[] embedding, object? metadata) =>
        StoreAsync(input, embedding, metadata).GetAwaiter().GetResult();

    public bool Store(string input, float[] embedding, object? metadata, TimeSpan? timeToLive) =>
        StoreAsync(input, embedding, metadata, timeToLive).GetAwaiter().GetResult();

    public bool Store(string input, string modelName, float[] embedding) =>
        StoreAsync(input, modelName, embedding).GetAwaiter().GetResult();

    public bool Store(string input, string modelName, float[] embedding, TimeSpan? timeToLive) =>
        StoreAsync(input, modelName, embedding, metadata: null, timeToLive).GetAwaiter().GetResult();

    public bool Store(string input, string modelName, float[] embedding, object? metadata) =>
        StoreAsync(input, modelName, embedding, metadata).GetAwaiter().GetResult();

    public bool Store(string input, string modelName, float[] embedding, object? metadata, TimeSpan? timeToLive) =>
        StoreAsync(input, modelName, embedding, metadata, timeToLive).GetAwaiter().GetResult();

    public EmbeddingsCacheEntry Set(string input, float[] embedding) =>
        SetAsync(input, embedding).GetAwaiter().GetResult();

    public EmbeddingsCacheEntry Set(string input, float[] embedding, TimeSpan? timeToLive) =>
        SetAsync(input, embedding, metadata: null, timeToLive).GetAwaiter().GetResult();

    public EmbeddingsCacheEntry Set(string input, float[] embedding, object? metadata) =>
        SetAsync(input, embedding, metadata).GetAwaiter().GetResult();

    public EmbeddingsCacheEntry Set(string input, float[] embedding, object? metadata, TimeSpan? timeToLive) =>
        SetAsync(input, embedding, metadata, timeToLive).GetAwaiter().GetResult();

    public EmbeddingsCacheEntry Set(string input, string modelName, float[] embedding) =>
        SetAsync(input, modelName, embedding).GetAwaiter().GetResult();

    public EmbeddingsCacheEntry Set(string input, string modelName, float[] embedding, TimeSpan? timeToLive) =>
        SetAsync(input, modelName, embedding, metadata: null, timeToLive).GetAwaiter().GetResult();

    public EmbeddingsCacheEntry Set(string input, string modelName, float[] embedding, object? metadata) =>
        SetAsync(input, modelName, embedding, metadata).GetAwaiter().GetResult();

    public EmbeddingsCacheEntry Set(string input, string modelName, float[] embedding, object? metadata, TimeSpan? timeToLive) =>
        SetAsync(input, modelName, embedding, metadata, timeToLive).GetAwaiter().GetResult();

    public bool StoreMany(IReadOnlyList<EmbeddingsCacheWriteRequest> entries) =>
        StoreManyAsync(entries).GetAwaiter().GetResult();

    public IReadOnlyList<EmbeddingsCacheEntry> SetMany(IReadOnlyList<EmbeddingsCacheWriteRequest> entries) =>
        SetManyAsync(entries).GetAwaiter().GetResult();

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

    public async Task<IReadOnlyList<EmbeddingsCacheEntry>> SetManyAsync(
        IReadOnlyList<EmbeddingsCacheWriteRequest> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return [];
        }

        var results = new EmbeddingsCacheEntry[entries.Count];
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var normalizedModelName = entry.ModelName is null ? null : NormalizeModelName(entry.ModelName);
            results[index] = await SetAsyncCore(
                entry.Input,
                entry.Embedding,
                normalizedModelName,
                entry.Metadata,
                entry.TimeToLive,
                cancellationToken).ConfigureAwait(false);
        }

        return results;
    }

    public EmbeddingsCacheEntry? Get(string input) =>
        GetAsync(input).GetAwaiter().GetResult();

    public EmbeddingsCacheEntry? Get(string input, string modelName) =>
        GetAsync(input, modelName).GetAwaiter().GetResult();

    public IReadOnlyList<EmbeddingsCacheEntry?> GetMany(IReadOnlyList<EmbeddingsCacheLookup> lookups) =>
        GetManyAsync(lookups).GetAwaiter().GetResult();

    public EmbeddingsCacheEntry? GetByKey(string key) =>
        GetByKeyAsync(key).GetAwaiter().GetResult();

    public IReadOnlyList<EmbeddingsCacheEntry?> GetManyByKey(IReadOnlyList<string> keys) =>
        GetManyByKeyAsync(keys).GetAwaiter().GetResult();

    public EmbeddingsCacheEntry? Lookup(string input) =>
        Get(input);

    public EmbeddingsCacheEntry? Lookup(string input, string modelName) =>
        Get(input, modelName);

    public IReadOnlyList<EmbeddingsCacheEntry?> LookupMany(IReadOnlyList<EmbeddingsCacheLookup> lookups) =>
        GetMany(lookups);

    public float[]? LookupEmbedding(string input) =>
        Lookup(input)?.Embedding;

    public float[]? LookupEmbedding(string input, string modelName) =>
        Lookup(input, modelName)?.Embedding;

    public bool Exists(string input) =>
        ExistsAsync(input).GetAwaiter().GetResult();

    public bool Exists(string input, string modelName) =>
        ExistsAsync(input, modelName).GetAwaiter().GetResult();

    public IReadOnlyList<bool> ExistsMany(IReadOnlyList<EmbeddingsCacheLookup> lookups) =>
        ExistsManyAsync(lookups).GetAwaiter().GetResult();

    public bool ExistsByKey(string key) =>
        ExistsByKeyAsync(key).GetAwaiter().GetResult();

    public IReadOnlyList<bool> ExistsManyByKey(IReadOnlyList<string> keys) =>
        ExistsManyByKeyAsync(keys).GetAwaiter().GetResult();

    public bool Delete(string input) =>
        DeleteAsync(input).GetAwaiter().GetResult();

    public bool Delete(string input, string modelName) =>
        DeleteAsync(input, modelName).GetAwaiter().GetResult();

    public long DeleteMany(IReadOnlyList<EmbeddingsCacheLookup> lookups) =>
        DeleteManyAsync(lookups).GetAwaiter().GetResult();

    public bool DeleteByKey(string key) =>
        DeleteByKeyAsync(key).GetAwaiter().GetResult();

    public long DeleteManyByKey(IReadOnlyList<string> keys) =>
        DeleteManyByKeyAsync(keys).GetAwaiter().GetResult();

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

        var results = new EmbeddingsCacheEntry?[lookups.Count];
        for (var index = 0; index < lookups.Count; index++)
        {
            var lookup = lookups[index];
            results[index] = lookup.ModelName is null
                ? await GetAsync(lookup.Input, cancellationToken).ConfigureAwait(false)
                : await GetAsync(lookup.Input, lookup.ModelName, cancellationToken).ConfigureAwait(false);
        }

        return results;
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

        var results = new EmbeddingsCacheEntry?[keys.Count];
        for (var index = 0; index < keys.Count; index++)
        {
            results[index] = await GetByKeyAsync(keys[index], cancellationToken).ConfigureAwait(false);
        }

        return results;
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

        var results = new bool[lookups.Count];
        for (var index = 0; index < lookups.Count; index++)
        {
            var lookup = lookups[index];
            results[index] = lookup.ModelName is null
                ? await ExistsAsync(lookup.Input, cancellationToken).ConfigureAwait(false)
                : await ExistsAsync(lookup.Input, lookup.ModelName, cancellationToken).ConfigureAwait(false);
        }

        return results;
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

        var results = new bool[keys.Count];
        for (var index = 0; index < keys.Count; index++)
        {
            results[index] = await ExistsByKeyAsync(keys[index], cancellationToken).ConfigureAwait(false);
        }

        return results;
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

        var deletedCount = 0L;
        for (var index = 0; index < lookups.Count; index++)
        {
            var lookup = lookups[index];
            var deleted = lookup.ModelName is null
                ? await DeleteAsync(lookup.Input, cancellationToken).ConfigureAwait(false)
                : await DeleteAsync(lookup.Input, lookup.ModelName, cancellationToken).ConfigureAwait(false);
            if (deleted)
            {
                deletedCount++;
            }
        }

        return deletedCount;
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

        var deletedCount = 0L;
        for (var index = 0; index < keys.Count; index++)
        {
            if (await DeleteByKeyAsync(keys[index], cancellationToken).ConfigureAwait(false))
            {
                deletedCount++;
            }
        }

        return deletedCount;
    }

    internal RedisKey CreateKey(string input) => CreateKey(input, modelName: null);

    internal RedisKey CreateKey(string input, string? modelName)
    {
        var identity = string.IsNullOrEmpty(modelName)
            ? input
            : string.Concat(input, KeyHashSeparator, modelName);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
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

        var metadataPayload = SerializeMetadata(metadata);
        if (metadataPayload is not null)
        {
            entries.Add(new HashEntry(MetadataFieldName, metadataPayload));
        }

        var key = CreateKey(normalizedInput, modelName);
        await _database.HashSetAsync(key, entries.ToArray()).WaitAsync(cancellationToken).ConfigureAwait(false);

        var effectiveTimeToLive = timeToLive ?? TimeToLive;
        if (effectiveTimeToLive.HasValue)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _database.KeyExpireAsync(key, effectiveTimeToLive).WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return new EmbeddingsCacheEntry(
            normalizedInput,
            embedding,
            modelName,
            metadataPayload,
            key);
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
