namespace RedisVlDotNet.Rerankers;

public sealed class RerankRequest
{
    public RerankRequest(string query, IReadOnlyList<RerankDocument> documents, int? topN = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(documents);

        if (topN <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topN), "TopN must be greater than zero when provided.");
        }

        Query = query;
        Documents = documents;
        TopN = topN;
    }

    public string Query { get; }

    public IReadOnlyList<RerankDocument> Documents { get; }

    public int? TopN { get; }
}
