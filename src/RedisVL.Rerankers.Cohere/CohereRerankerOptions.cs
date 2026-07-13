namespace RedisVL.Rerankers.Cohere;

/// <summary>
/// Options controlling how <see cref="CohereTextReranker"/> requests reranking from Cohere.
/// </summary>
public sealed class CohereRerankerOptions
{
    private int? _maxTokensPerDocument;
    private int? _priority;

    /// <summary>
    /// The maximum number of tokens per document to consider when reranking. When <c>null</c>, the model default is used.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a non-positive value.</exception>
    public int? MaxTokensPerDocument
    {
        get => _maxTokensPerDocument;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "MaxTokensPerDocument must be greater than zero.");
            }

            _maxTokensPerDocument = value;
        }
    }

    /// <summary>
    /// An optional request priority between 0 and 999 passed to Cohere.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside the range 0 to 999.</exception>
    public int? Priority
    {
        get => _priority;
        init
        {
            if (value is < 0 or > 999)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Priority must be between 0 and 999.");
            }

            _priority = value;
        }
    }

    /// <summary>Optional value sent as the <c>X-Client-Name</c> header.</summary>
    public string? ClientName { get; init; }

    /// <summary>Overrides the default Cohere rerank endpoint.</summary>
    public string? EndpointOverride { get; init; }
}
