namespace RedisVL.Rerankers;

/// <summary>
/// Reorders a set of documents by their relevance to a query.
/// </summary>
public interface ITextReranker
{
    /// <summary>
    /// Reranks the documents in the request by relevance to its query.
    /// </summary>
    /// <param name="request">The query and candidate documents to rerank.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The reranked results, ordered from most to least relevant.</returns>
    Task<IReadOnlyList<RerankResult>> RerankAsync(
        RerankRequest request,
        CancellationToken cancellationToken = default);
}
