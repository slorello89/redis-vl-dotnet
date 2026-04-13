namespace RedisVlDotNet.Rerankers;

public interface ITextReranker
{
    Task<IReadOnlyList<RerankResult>> RerankAsync(
        RerankRequest request,
        CancellationToken cancellationToken = default);
}
