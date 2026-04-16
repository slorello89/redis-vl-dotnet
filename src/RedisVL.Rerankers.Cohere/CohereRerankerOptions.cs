namespace RedisVL.Rerankers.Cohere;

public sealed class CohereRerankerOptions
{
    private int? _maxTokensPerDocument;
    private int? _priority;

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

    public string? ClientName { get; init; }

    public string? EndpointOverride { get; init; }
}
