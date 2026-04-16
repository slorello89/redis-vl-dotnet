using RedisVL.Vectorizers.OpenAI;

namespace RedisVL.Tests.Vectorizers;

public sealed class OpenAiTextVectorizerSmokeTests
{
    [OpenAiIntegrationFact]
    public async Task VectorizeAsync_WithLiveOpenAiClient_ReturnsConfiguredDimensions()
    {
        var apiKey = OpenAiTestEnvironment.ApiKey!;
        var model = OpenAiTestEnvironment.Model ?? "text-embedding-3-small";
        var vectorizer = new OpenAiTextVectorizer(
            model,
            apiKey,
            new OpenAiVectorizerOptions
            {
                Dimensions = 8,
                EndUserId = "redis-vl-dotnet-tests"
            });

        var singleEmbedding = await vectorizer.VectorizeAsync("redis vector libraries");
        var batchEmbeddings = await vectorizer.VectorizeAsync(["redis", "vector"]);

        Assert.Equal(8, singleEmbedding.Length);
        Assert.Equal(2, batchEmbeddings.Count);
        Assert.All(batchEmbeddings, embedding => Assert.Equal(8, embedding.Length));
    }
}
