using RedisVL.Vectorizers.HuggingFace;

namespace RedisVL.Tests.Vectorizers;

public sealed class HuggingFaceTextVectorizerSmokeTests
{
    [HuggingFaceIntegrationFact]
    public async Task VectorizeAsync_WithLiveHuggingFaceClient_ReturnsEmbeddings()
    {
        var apiKey = HuggingFaceTestEnvironment.ApiKey!;
        var model = HuggingFaceTestEnvironment.Model ?? "intfloat/multilingual-e5-large";
        var vectorizer = new HuggingFaceTextVectorizer(
            model,
            apiKey,
            new HuggingFaceVectorizerOptions
            {
                Normalize = true
            });

        var singleEmbedding = await vectorizer.VectorizeAsync("redis vector libraries");
        var batchEmbeddings = await vectorizer.VectorizeAsync(["redis", "vector"]);

        Assert.NotEmpty(singleEmbedding);
        Assert.Equal(2, batchEmbeddings.Count);
        Assert.All(batchEmbeddings, Assert.NotEmpty);
    }
}
