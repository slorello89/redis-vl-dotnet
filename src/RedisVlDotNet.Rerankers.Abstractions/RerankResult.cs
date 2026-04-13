namespace RedisVlDotNet.Rerankers;

public sealed class RerankResult
{
    public RerankResult(int index, double score, RerankDocument document)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentNullException.ThrowIfNull(document);

        Index = index;
        Score = score;
        Document = document;
    }

    public int Index { get; }

    public double Score { get; }

    public RerankDocument Document { get; }
}
