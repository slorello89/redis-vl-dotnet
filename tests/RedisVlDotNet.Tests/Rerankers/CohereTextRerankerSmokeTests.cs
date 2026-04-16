using RedisVl.Rerankers;
using RedisVl.Rerankers.Cohere;

namespace RedisVl.Tests.Rerankers;

public sealed class CohereTextRerankerSmokeTests
{
    [CohereIntegrationFact]
    public async Task RerankAsync_WithLiveCohereClient_ReturnsOrderedMatches()
    {
        var apiKey = CohereTestEnvironment.ApiKey!;
        var model = CohereTestEnvironment.Model ?? "rerank-v4.0-pro";
        var reranker = new CohereTextReranker(
            model,
            apiKey,
            new CohereRerankerOptions
            {
                MaxTokensPerDocument = 512,
                ClientName = "redis-vl-dotnet-tests"
            });

        var results = await reranker.RerankAsync(
            new RerankRequest(
                "how do i reset my password",
                [
                    new RerankDocument("Open Settings and use the password reset flow.", id: "password"),
                    new RerankDocument("Download billing reports from the invoices page.", id: "billing"),
                    new RerankDocument("Manage API tokens from the developer access screen.", id: "tokens")
                ],
                topN: 2));

        Assert.Equal(2, results.Count);
        Assert.Equal("password", results[0].Document.Id);
        Assert.True(results[0].Score >= results[1].Score);
    }
}
