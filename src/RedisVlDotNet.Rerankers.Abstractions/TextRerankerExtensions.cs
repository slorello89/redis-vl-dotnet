namespace RedisVlDotNet.Rerankers;

public static class TextRerankerExtensions
{
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
