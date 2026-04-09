using RedisVlDotNet.Schema;

namespace RedisVlDotNet.Caches;

public sealed class SemanticCacheOptions
{
    public SemanticCacheOptions(
        string name,
        VectorFieldAttributes embeddingFieldAttributes,
        double distanceThreshold,
        string? keyNamespace = null,
        TimeSpan? timeToLive = null,
        string promptFieldName = "prompt",
        string responseFieldName = "response",
        string embeddingFieldName = "embedding")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(embeddingFieldAttributes);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingFieldName);

        if (distanceThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceThreshold), distanceThreshold, "Semantic cache distance threshold must be greater than zero.");
        }

        if (timeToLive.HasValue && timeToLive.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), "Semantic cache TTL must be positive when provided.");
        }

        Name = name.Trim();
        EmbeddingFieldAttributes = embeddingFieldAttributes;
        DistanceThreshold = distanceThreshold;
        KeyNamespace = string.IsNullOrWhiteSpace(keyNamespace) ? null : keyNamespace.Trim();
        TimeToLive = timeToLive;
        PromptFieldName = promptFieldName.Trim();
        ResponseFieldName = responseFieldName.Trim();
        EmbeddingFieldName = embeddingFieldName.Trim();
    }

    public string Name { get; }

    public VectorFieldAttributes EmbeddingFieldAttributes { get; }

    public double DistanceThreshold { get; }

    public string? KeyNamespace { get; }

    public TimeSpan? TimeToLive { get; }

    public string PromptFieldName { get; }

    public string ResponseFieldName { get; }

    public string EmbeddingFieldName { get; }
}
