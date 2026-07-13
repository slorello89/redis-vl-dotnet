namespace RedisVL.Vectorizers.Cohere;

/// <summary>
/// Configuration options for <see cref="CohereTextVectorizer"/>, controlling how text is
/// submitted to the Cohere embed API.
/// </summary>
public sealed class CohereVectorizerOptions
{
    private int? _outputDimension;

    /// <summary>
    /// The intended use of the inputs. Cohere requires an input type for its embedding models.
    /// Defaults to <see cref="CohereInputType.SearchDocument"/>.
    /// </summary>
    public CohereInputType InputType { get; init; } = CohereInputType.SearchDocument;

    /// <summary>
    /// Requests a specific output dimensionality from models that support it (for example, <c>embed-v4.0</c>).
    /// </summary>
    public int? OutputDimension
    {
        get => _outputDimension;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "OutputDimension must be greater than zero.");
            }

            _outputDimension = value;
        }
    }

    /// <summary>
    /// Controls how inputs that exceed the model's maximum token length are handled.
    /// </summary>
    public CohereTruncate? Truncate { get; init; }

    /// <summary>
    /// Optional value sent as the <c>X-Client-Name</c> header.
    /// </summary>
    public string? ClientName { get; init; }

    /// <summary>
    /// Overrides the default Cohere embed endpoint.
    /// </summary>
    public string? EndpointOverride { get; init; }
}
