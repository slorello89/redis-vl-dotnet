using RedisVL.Rerankers;
using RedisVL.Rerankers.Onnx;

namespace RedisVL.Tests.Rerankers;

public sealed class OnnxTextRerankerSmokeTests
{
    [OnnxIntegrationFact]
    public async Task RerankAsync_WithLocalOnnxAssets_ReturnsOrderedMatches()
    {
        using var reranker = new OnnxTextReranker(
            new OnnxRerankerOptions
            {
                ModelPath = OnnxTestEnvironment.ModelPath!,
                TokenizerPath = OnnxTestEnvironment.TokenizerPath!,
                MaxSequenceLength = 512
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
