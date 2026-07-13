namespace RedisVL.Vectorizers.HuggingFace;

/// <summary>
/// Specifies the side from which Hugging Face truncates inputs that exceed the model's maximum length.
/// </summary>
public enum HuggingFaceTruncationDirection
{
    /// <summary>Truncate from the beginning (left side) of the input.</summary>
    Left,

    /// <summary>Truncate from the end (right side) of the input.</summary>
    Right
}
