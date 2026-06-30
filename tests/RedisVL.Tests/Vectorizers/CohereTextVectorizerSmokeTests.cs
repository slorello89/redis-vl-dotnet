using RedisVL.Vectorizers.Cohere;

namespace RedisVL.Tests.Vectorizers;

public sealed class CohereTextVectorizerSmokeTests
{
    [CohereVectorizerIntegrationFact]
    public async Task VectorizeAsync_WithLiveCohereClient_ReturnsEmbeddings()
    {
        var apiKey = CohereVectorizerTestEnvironment.ApiKey!;
        var model = CohereVectorizerTestEnvironment.Model ?? "embed-english-v3.0";
        var vectorizer = new CohereTextVectorizer(
            model,
            apiKey,
            new CohereVectorizerOptions
            {
                InputType = CohereInputType.SearchDocument,
                ClientName = "redis-vl-dotnet-tests"
            });

        var singleEmbedding = await vectorizer.VectorizeAsync("redis vector libraries");
        var batchEmbeddings = await vectorizer.VectorizeAsync(["redis", "vector"]);

        Assert.NotEmpty(singleEmbedding);
        Assert.Equal(2, batchEmbeddings.Count);
        Assert.All(batchEmbeddings, Assert.NotEmpty);
    }
}
