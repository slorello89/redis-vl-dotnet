namespace RedisVL.Caches;

/// <summary>A cached embedding together with the input it was generated from and its associated metadata.</summary>
public sealed class EmbeddingsCacheEntry
{
    /// <summary>Initializes a new <see cref="EmbeddingsCacheEntry" />.</summary>
    /// <param name="input">The source text the embedding was generated from.</param>
    /// <param name="embedding">The embedding vector; copied defensively into the entry.</param>
    /// <param name="modelName">The optional embedding model name; blank values are treated as <see langword="null" />.</param>
    /// <param name="metadata">The optional serialized metadata stored alongside the embedding.</param>
    /// <param name="key">The optional Redis key the entry is stored under; blank values are treated as <see langword="null" />.</param>
    /// <exception cref="ArgumentException"><paramref name="input" /> is <see langword="null" />, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="embedding" /> is <see langword="null" />.</exception>
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

    /// <summary>Gets the source text the embedding was generated from.</summary>
    public string Input { get; }

    /// <summary>Gets the embedding model name, or <see langword="null" /> when the entry is not model-scoped.</summary>
    public string? ModelName { get; }

    /// <summary>Gets the cached embedding vector.</summary>
    public float[] Embedding { get; }

    /// <summary>Gets the serialized metadata stored alongside the embedding, or <see langword="null" /> when none.</summary>
    public string? Metadata { get; }

    /// <summary>Gets the Redis key the entry is stored under, or <see langword="null" /> when unknown.</summary>
    public string? Key { get; }
}
