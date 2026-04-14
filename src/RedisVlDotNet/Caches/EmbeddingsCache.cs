using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;

namespace RedisVl.Caches;

public sealed class EmbeddingsCache
{
    private const string InputFieldName = "input";
    private const string EmbeddingFieldName = "embedding";

    private readonly IDatabase _database;

    public EmbeddingsCache(IDatabase database, EmbeddingsCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        _database = database;
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

        HashEntry[] entries =
        [
            new HashEntry(InputFieldName, normalizedInput),
            new HashEntry(EmbeddingFieldName, EncodeFloat32(embedding))
        ];

        var key = CreateKey(normalizedInput);
        await _database.HashSetAsync(key, entries).WaitAsync(cancellationToken).ConfigureAwait(false);

        if (TimeToLive.HasValue)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _database.KeyExpireAsync(key, TimeToLive).WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    public float[]? Lookup(string input) =>
        LookupAsync(input).GetAwaiter().GetResult();

    public async Task<float[]?> LookupAsync(string input, CancellationToken cancellationToken = default)
    {
        var normalizedInput = NormalizeInput(input);

        cancellationToken.ThrowIfCancellationRequested();

        var entries = await _database
            .HashGetAllAsync(CreateKey(normalizedInput))
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        if (entries.Length == 0)
        {
            return null;
        }

        string? cachedInput = null;
        byte[]? payload = null;

        foreach (var entry in entries)
        {
            if (entry.Name == InputFieldName)
            {
                cachedInput = entry.Value;
                continue;
            }

            if (entry.Name == EmbeddingFieldName && !entry.Value.IsNull)
            {
                payload = (byte[]?)entry.Value;
            }
        }

        if (!string.Equals(cachedInput, normalizedInput, StringComparison.Ordinal) || payload is null)
        {
            return null;
        }

        return DecodeFloat32(payload);
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
}
