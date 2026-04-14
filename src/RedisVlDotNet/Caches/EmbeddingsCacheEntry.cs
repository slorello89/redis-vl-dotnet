namespace RedisVl.Caches;

public sealed class EmbeddingsCacheEntry
{
    public EmbeddingsCacheEntry(
        string input,
        float[] embedding,
        string? modelName = null,
        string? metadata = null,
        string? key = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentNullException.ThrowIfNull(embedding);

        Input = input;
        Embedding = embedding.ToArray();
        ModelName = string.IsNullOrWhiteSpace(modelName) ? null : modelName;
        Metadata = metadata;
        Key = string.IsNullOrWhiteSpace(key) ? null : key;
    }

    public string Input { get; }

    public string? ModelName { get; }

    public float[] Embedding { get; }

    public string? Metadata { get; }

    public string? Key { get; }
}
