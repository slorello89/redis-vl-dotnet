namespace RedisVL.Rerankers;

/// <summary>
/// Represents a single reranked document together with its relevance score and original position.
/// </summary>
public sealed class RerankResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RerankResult"/> class.
    /// </summary>
    /// <param name="index">The zero-based index of the document in the original request.</param>
    /// <param name="score">The relevance score assigned by the reranker.</param>
    /// <param name="document">The document that was scored.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is <c>null</c>.</exception>
    public RerankResult(int index, double score, RerankDocument document)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(document);

        Index = index;
        Score = score;
        Document = document;
    }

    /// <summary>Gets the zero-based index of the document in the original request.</summary>
    public int Index { get; }

    /// <summary>Gets the relevance score assigned by the reranker; higher indicates more relevant.</summary>
    public double Score { get; }

    /// <summary>Gets the document that was scored.</summary>
    public RerankDocument Document { get; }
}
