namespace RedisVL.Caches;

/// <summary>A single embedding entry to store as part of a batch <c>StoreMany</c>/<c>SetMany</c> call.</summary>
public readonly record struct EmbeddingsCacheWriteRequest
{
    /// <summary>Initializes a new <see cref="EmbeddingsCacheWriteRequest" />.</summary>
    /// <param name="input">The source text the embedding was generated from.</param>
    /// <param name="embedding">The embedding vector to store.</param>
    /// <param name="modelName">The optional embedding model name that scopes the entry; blank values are treated as <see langword="null" />.</param>
    /// <param name="metadata">Optional metadata serialized and stored alongside the embedding.</param>
    /// <param name="timeToLive">An optional per-entry expiry that overrides the cache default; must be positive when provided.</param>
    /// <exception cref="ArgumentException"><paramref name="input" /> is <see langword="null" />, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="embedding" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeToLive" /> is zero or negative.</exception>
    public EmbeddingsCacheWriteRequest(
        string input,
        float[] embedding,
        string? modelName = null,
        object? metadata = null,
        TimeSpan? timeToLive = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentNullException.ThrowIfNull(embedding);
        if (timeToLive.HasValue && timeToLive.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), "Cache TTL must be positive when provided.");
        }

        Input = input;
        Embedding = embedding;
        ModelName = string.IsNullOrWhiteSpace(modelName) ? null : modelName;
        Metadata = metadata;
        TimeToLive = timeToLive;
    }

    /// <summary>Gets the source text the embedding was generated from.</summary>
    public string Input { get; }

    /// <summary>Gets the embedding model name that scopes the entry, or <see langword="null" /> when unscoped.</summary>
    public string? ModelName { get; }

    /// <summary>Gets the embedding vector to store.</summary>
    public float[] Embedding { get; }

    /// <summary>Gets the metadata to serialize and store alongside the embedding, or <see langword="null" /> when none.</summary>
    public object? Metadata { get; }

    /// <summary>Gets the per-entry expiry that overrides the cache default, or <see langword="null" /> to use the default.</summary>
    public TimeSpan? TimeToLive { get; }
}
