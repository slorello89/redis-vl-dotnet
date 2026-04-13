namespace RedisVlDotNet.Vectorizers.OpenAI;

public sealed class OpenAiVectorizerOptions
{
    private int? _dimensions;

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

    public string? EndUserId { get; init; }
}
