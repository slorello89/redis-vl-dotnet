namespace RedisVL.Workflows;

/// <summary>A message returned by <see cref="SemanticMessageHistory" /> relevance queries, paired with its distance from the query.</summary>
public sealed class SemanticMessageHistoryMatch
{
    /// <summary>Initializes a new <see cref="SemanticMessageHistoryMatch" />.</summary>
    /// <param name="message">The matched message.</param>
    /// <param name="distance">The distance between the query embedding and the message; must be non-negative.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="distance" /> is negative.</exception>
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

    /// <summary>Gets the matched message.</summary>
    public MessageHistoryMessage Message { get; }

    /// <summary>Gets the distance between the query embedding and the message; smaller is more relevant.</summary>
    public double Distance { get; }
}
