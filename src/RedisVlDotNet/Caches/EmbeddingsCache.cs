using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;

namespace RedisVl.Caches;

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

    public bool Store(string input, float[] embedding, object? metadata) =>
        StoreAsync(input, embedding, metadata).GetAwaiter().GetResult();

    public bool Store(string input, string modelName, float[] embedding) =>
        StoreAsync(input, modelName, embedding).GetAwaiter().GetResult();

    public bool Store(string input, string modelName, float[] embedding, object? metadata) =>
        StoreAsync(input, modelName, embedding, metadata).GetAwaiter().GetResult();

    public async Task<bool> StoreAsync(string input, float[] embedding, CancellationToken cancellationToken = default)
    {
        return await StoreAsyncCore(input, embedding, modelName: null, metadata: null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> StoreAsync(
        string input,
        float[] embedding,
        object? metadata,
        CancellationToken cancellationToken = default)
    {
        return await StoreAsyncCore(input, embedding, modelName: null, metadata, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> StoreAsync(
        string input,
        string modelName,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        return await StoreAsyncCore(
            input,
            embedding,
            NormalizeModelName(modelName),
            metadata: null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> StoreAsync(
        string input,
        string modelName,
        float[] embedding,
        object? metadata,
        CancellationToken cancellationToken = default)
    {
        return await StoreAsyncCore(
            input,
            embedding,
            NormalizeModelName(modelName),
            metadata,
            cancellationToken).ConfigureAwait(false);
    }

    public EmbeddingsCacheEntry? Lookup(string input) =>
        LookupAsync(input).GetAwaiter().GetResult();

    public EmbeddingsCacheEntry? Lookup(string input, string modelName) =>
        LookupAsync(input, modelName).GetAwaiter().GetResult();

    public float[]? LookupEmbedding(string input) =>
        Lookup(input)?.Embedding;

    public float[]? LookupEmbedding(string input, string modelName) =>
        Lookup(input, modelName)?.Embedding;

    public async Task<EmbeddingsCacheEntry?> LookupAsync(string input, CancellationToken cancellationToken = default)
    {
        return await LookupAsyncCore(input, modelName: null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EmbeddingsCacheEntry?> LookupAsync(
        string input,
        string modelName,
        CancellationToken cancellationToken = default)
    {
        return await LookupAsyncCore(
            input,
            NormalizeModelName(modelName),
            cancellationToken).ConfigureAwait(false);
    }

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

    private async Task<bool> StoreAsyncCore(
        string input,
        float[] embedding,
        string? modelName,
        object? metadata,
        CancellationToken cancellationToken)
    {
        var normalizedInput = NormalizeInput(input);
        ArgumentNullException.ThrowIfNull(embedding);

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

        if (TimeToLive.HasValue)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _database.KeyExpireAsync(key, TimeToLive).WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private async Task<EmbeddingsCacheEntry?> LookupAsyncCore(
        string input,
        string? modelName,
        CancellationToken cancellationToken)
    {
        var normalizedInput = NormalizeInput(input);

        cancellationToken.ThrowIfCancellationRequested();

        var entries = await _database
            .HashGetAllAsync(CreateKey(normalizedInput, modelName))
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

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

        var normalizedCachedModelName = string.IsNullOrEmpty(cachedModelName) ? null : cachedModelName;
        if (!string.Equals(cachedInput, normalizedInput, StringComparison.Ordinal) ||
            !string.Equals(normalizedCachedModelName, modelName, StringComparison.Ordinal) ||
            payload is null)
        {
            return null;
        }

        return new EmbeddingsCacheEntry(
            normalizedInput,
            DecodeFloat32(payload),
            normalizedCachedModelName,
            metadata,
            CreateKey(normalizedInput, modelName));
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

    private string? SerializeMetadata(object? metadata) =>
        metadata is null ? null : JsonSerializer.Serialize(metadata, _serializerOptions);
}
