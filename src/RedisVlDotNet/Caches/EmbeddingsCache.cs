using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;

namespace RedisVlDotNet.Caches;

public sealed class EmbeddingsCache
{
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

    public async Task<bool> StoreAsync(string input, float[] embedding, CancellationToken cancellationToken = default)
    {
        var normalizedInput = NormalizeInput(input);
        ArgumentNullException.ThrowIfNull(embedding);

        cancellationToken.ThrowIfCancellationRequested();

        var payload = JsonSerializer.Serialize(
            new CachePayload(normalizedInput, EncodeFloat32(embedding)),
            _serializerOptions);

        return await _database
            .StringSetAsync(CreateKey(normalizedInput), payload, TimeToLive, When.Always)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public float[]? Lookup(string input) =>
        LookupAsync(input).GetAwaiter().GetResult();

    public async Task<float[]?> LookupAsync(string input, CancellationToken cancellationToken = default)
    {
        var normalizedInput = NormalizeInput(input);

        cancellationToken.ThrowIfCancellationRequested();

        var payload = await _database
            .StringGetAsync(CreateKey(normalizedInput))
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        if (payload.IsNullOrEmpty)
        {
            return null;
        }

        var cachedPayload = JsonSerializer.Deserialize<CachePayload>(payload!, _serializerOptions);
        if (cachedPayload is null || !string.Equals(cachedPayload.Input, normalizedInput, StringComparison.Ordinal))
        {
            return null;
        }

        return DecodeFloat32(cachedPayload.Embedding);
    }

    internal RedisKey CreateKey(string input)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
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

    private static string NormalizeInput(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        return input;
    }

    private sealed record CachePayload(string Input, byte[] Embedding);
}
