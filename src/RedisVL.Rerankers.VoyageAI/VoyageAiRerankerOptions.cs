namespace RedisVL.Rerankers.VoyageAI;

public sealed class VoyageAiRerankerOptions
{
    /// <summary>
    /// Whether Voyage AI should truncate inputs that exceed the model's context length. When null,
    /// the provider default (truncation enabled) applies.
    /// </summary>
    public bool? Truncation { get; init; }

    /// <summary>Overrides the default rerank endpoint when set.</summary>
    public string? EndpointOverride { get; init; }
}
