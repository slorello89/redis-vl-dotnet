namespace RedisVlDotNet.Caches;

public readonly record struct EmbeddingsCacheWriteRequest
{
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

    public string Input { get; }

    public string? ModelName { get; }

    public float[] Embedding { get; }

    public object? Metadata { get; }

    public TimeSpan? TimeToLive { get; }
}
