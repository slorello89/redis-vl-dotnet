namespace RedisVL.Vectorizers.OpenAI;

/// <summary>
/// Options controlling how <see cref="OpenAiTextVectorizer"/> requests embeddings from OpenAI.
/// </summary>
public sealed class OpenAiVectorizerOptions
{
    private int? _dimensions;

    /// <summary>
    /// The number of dimensions the resulting embeddings should have, for models that support
    /// dimensionality reduction. When <c>null</c>, the model's default dimensionality is used.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a non-positive value.</exception>
    public int? Dimensions
    {
        get => _dimensions;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Dimensions must be greater than zero.");
            }

            _dimensions = value;
        }
    }

    /// <summary>
    /// An optional stable end-user identifier passed to OpenAI to help with abuse monitoring.
    /// </summary>
    public string? EndUserId { get; init; }
}
