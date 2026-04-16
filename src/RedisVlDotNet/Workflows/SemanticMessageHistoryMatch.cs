namespace RedisVl.Workflows;

public sealed class SemanticMessageHistoryMatch
{
    public SemanticMessageHistoryMatch(MessageHistoryMessage message, double distance)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (distance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distance), distance, "Semantic message distance cannot be negative.");
        }

        Message = message;
        Distance = distance;
    }

    public MessageHistoryMessage Message { get; }

    public double Distance { get; }
}
