namespace RedisVlDotNet.Vectorizers.HuggingFace;

public sealed class HuggingFaceVectorizerOptions
{
    public bool? Normalize { get; init; }

    public string? PromptName { get; init; }

    public bool? Truncate { get; init; }

    public HuggingFaceTruncationDirection? TruncationDirection { get; init; }

    public string? EndpointOverride { get; init; }
}
