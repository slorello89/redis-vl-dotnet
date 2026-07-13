namespace RedisVL.Rerankers;

/// <summary>
/// Extension methods for <see cref="ITextReranker"/>.
/// </summary>
public static class TextRerankerExtensions
{
    /// <summary>
    /// Reranks plain-text documents against a query, wrapping each string in a <see cref="RerankDocument"/>.
    /// </summary>
    /// <param name="reranker">The reranker to use.</param>
    /// <param name="query">The query the documents are ranked against.</param>
    /// <param name="documents">The candidate document texts to rerank.</param>
    /// <param name="topN">The maximum number of results to return, or <c>null</c> to return all.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The reranked results, ordered from most to least relevant.</returns>
    public static async Task<IReadOnlyList<RerankResult>> RerankAsync(
        this ITextReranker reranker,
        string query,
        IReadOnlyList<string> documents,
        int? topN = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reranker);
        ArgumentNullException.ThrowIfNull(documents);

        if (documents.Count == 0)
        {
            return [];
        }

        var rerankDocuments = new RerankDocument[documents.Count];
        for (var index = 0; index < documents.Count; index++)
        {
            rerankDocuments[index] = new RerankDocument(documents[index]);
        }

        return await reranker.RerankAsync(
            new RerankRequest(query, rerankDocuments, topN),
            cancellationToken).ConfigureAwait(false);
    }
}
