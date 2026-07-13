namespace RedisVL.Caches;

/// <summary>Identifies a single cached embedding to look up by its source input and optional model name.</summary>
public readonly record struct EmbeddingsCacheLookup
{
    /// <summary>Initializes a new <see cref="EmbeddingsCacheLookup" />.</summary>
    /// <param name="input">The source text whose cached embedding is being looked up.</param>
    /// <param name="modelName">The optional embedding model name that scopes the lookup; blank values are treated as <see langword="null" />.</param>
    /// <exception cref="ArgumentException"><paramref name="input" /> is <see langword="null" />, empty, or whitespace.</exception>
    public EmbeddingsCacheLookup(string input, string? modelName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        Input = input;
        ModelName = string.IsNullOrWhiteSpace(modelName) ? null : modelName;
    }

    /// <summary>Gets the source text whose cached embedding is being looked up.</summary>
    public string Input { get; }

    /// <summary>Gets the embedding model name that scopes the lookup, or <see langword="null" /> when unscoped.</summary>
    public string? ModelName { get; }
}
