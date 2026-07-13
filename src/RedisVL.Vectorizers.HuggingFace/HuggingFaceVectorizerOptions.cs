namespace RedisVL.Vectorizers.HuggingFace;

/// <summary>
/// Options controlling how <see cref="HuggingFaceTextVectorizer"/> requests feature-extraction embeddings.
/// </summary>
public sealed class HuggingFaceVectorizerOptions
{
    /// <summary>Whether the model should L2-normalize the returned embeddings. When <c>null</c>, the model default is used.</summary>
    public bool? Normalize { get; init; }

    /// <summary>An optional named prompt template configured on the model to prepend to each input.</summary>
    public string? PromptName { get; init; }

    /// <summary>Whether inputs that exceed the model's maximum length should be truncated. When <c>null</c>, the model default is used.</summary>
    public bool? Truncate { get; init; }

    /// <summary>The side from which inputs are truncated when <see cref="Truncate"/> is enabled.</summary>
    public HuggingFaceTruncationDirection? TruncationDirection { get; init; }

    /// <summary>Overrides the default Hugging Face inference endpoint URL.</summary>
    public string? EndpointOverride { get; init; }
}
