namespace RedisVL.Rerankers;

/// <summary>
/// Describes a reranking request: a query and the candidate documents to score against it.
/// </summary>
public sealed class RerankRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RerankRequest"/> class.
    /// </summary>
    /// <param name="query">The query the documents are ranked against.</param>
    /// <param name="documents">The candidate documents to rerank.</param>
    /// <param name="topN">The maximum number of results to return, or <c>null</c> to return all.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="query"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="documents"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="topN"/> is not greater than zero.</exception>
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

    /// <summary>Gets the query the documents are ranked against.</summary>
    public string Query { get; }

    /// <summary>Gets the candidate documents to rerank.</summary>
    public IReadOnlyList<RerankDocument> Documents { get; }

    /// <summary>Gets the maximum number of results to return, or <c>null</c> to return all.</summary>
    public int? TopN { get; }
}
