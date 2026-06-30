using System.Net;
using System.Net.Http.Headers;
using System.Text;
using RedisVL.Vectorizers.Cohere;

namespace RedisVL.Tests.Vectorizers;

public sealed class CohereTextVectorizerTests
{
    [Fact]
    public async Task VectorizeAsync_WithSingleInput_UsesConfiguredRequestShape()
    {
        var handler = new RecordingHttpMessageHandler("""{"embeddings":{"float":[[1.5,2.5,3.5]]}}""");
        using var client = new HttpClient(handler);
        var vectorizer = new CohereTextVectorizer(
            "embed-english-v3.0",
            "cohere_test_token",
            new CohereVectorizerOptions
            {
                InputType = CohereInputType.SearchQuery,
                OutputDimension = 1024,
                Truncate = CohereTruncate.End,
                ClientName = "redis-vl-dotnet-tests"
            },
            client);

        var embedding = await vectorizer.VectorizeAsync("hello world");

        Assert.Equal([1.5f, 2.5f, 3.5f], embedding);
        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.cohere.com/v2/embed", handler.Request.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "cohere_test_token"), handler.Request.Headers.Authorization);
        Assert.Equal("redis-vl-dotnet-tests", handler.Request.Headers.GetValues("X-Client-Name").Single());
        Assert.Equal(
            """{"model":"embed-english-v3.0","texts":["hello world"],"input_type":"search_query","embedding_types":["float"],"output_dimension":1024,"truncate":"END"}""",
            handler.RequestBody);
    }

    [Fact]
    public async Task VectorizeAsync_WithMultipleInputs_UsesBatchPayloadWithDefaultInputType()
    {
        var handler = new RecordingHttpMessageHandler("""{"embeddings":{"float":[[1.0,2.0],[3.0,4.0]]}}""");
        using var client = new HttpClient(handler);
        var vectorizer = new CohereTextVectorizer("embed-english-v3.0", "cohere_test_token", httpClient: client);

        var embeddings = await vectorizer.VectorizeAsync(["alpha", "beta"]);

        Assert.Equal(2, embeddings.Count);
        Assert.Equal([1f, 2f], embeddings[0]);
        Assert.Equal([3f, 4f], embeddings[1]);
        Assert.Equal(
            """{"model":"embed-english-v3.0","texts":["alpha","beta"],"input_type":"search_document","embedding_types":["float"]}""",
            handler.RequestBody);
    }

    [Fact]
    public async Task VectorizeAsync_WithEmptyBatch_ReturnsEmptyWithoutCallingHttp()
    {
        var handler = new RecordingHttpMessageHandler("""{"embeddings":{"float":[[1.0]]}}""");
        using var client = new HttpClient(handler);
        var vectorizer = new CohereTextVectorizer("embed-english-v3.0", "cohere_test_token", httpClient: client);

        var embeddings = await vectorizer.VectorizeAsync([]);

        Assert.Empty(embeddings);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task VectorizeAsync_WithEndpointOverride_UsesOverrideUri()
    {
        var handler = new RecordingHttpMessageHandler("""{"embeddings":{"float":[[42.0]]}}""");
        using var client = new HttpClient(handler);
        var vectorizer = new CohereTextVectorizer(
            "embed-english-v3.0",
            "cohere_test_token",
            new CohereVectorizerOptions
            {
                EndpointOverride = "https://example.test/embed"
            },
            client);

        await vectorizer.VectorizeAsync("alpha");

        Assert.Equal("https://example.test/embed", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task VectorizeAsync_WithMismatchedBatchResponse_ThrowsInvalidOperationException()
    {
        var handler = new RecordingHttpMessageHandler("""{"embeddings":{"float":[[1.0,2.0]]}}""");
        using var client = new HttpClient(handler);
        var vectorizer = new CohereTextVectorizer("embed-english-v3.0", "cohere_test_token", httpClient: client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => vectorizer.VectorizeAsync(["alpha", "beta"]));

        Assert.Equal("Cohere embeddings response count did not match the number of requested inputs.", exception.Message);
    }

    [Fact]
    public async Task VectorizeAsync_WithMissingFloatEmbeddings_ThrowsInvalidOperationException()
    {
        var handler = new RecordingHttpMessageHandler("""{"embeddings":{}}""");
        using var client = new HttpClient(handler);
        var vectorizer = new CohereTextVectorizer("embed-english-v3.0", "cohere_test_token", httpClient: client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => vectorizer.VectorizeAsync("alpha"));

        Assert.Equal("Cohere embed response did not contain float embeddings.", exception.Message);
    }

    [Fact]
    public async Task VectorizeAsync_WithErrorResponse_ThrowsInvalidOperationException()
    {
        var handler = new RecordingHttpMessageHandler("""{"message":"invalid token"}""", HttpStatusCode.Unauthorized);
        using var client = new HttpClient(handler);
        var vectorizer = new CohereTextVectorizer("embed-english-v3.0", "cohere_test_token", httpClient: client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => vectorizer.VectorizeAsync("alpha"));

        Assert.Contains("401", exception.Message);
        Assert.Contains("invalid token", exception.Message);
    }

    [Fact]
    public void CohereVectorizerOptions_WithInvalidOutputDimension_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new CohereVectorizerOptions { OutputDimension = 0 });
        Assert.Contains("OutputDimension must be greater than zero.", exception.Message);
    }

    [Fact]
    public void Constructor_WithWhitespaceApiKey_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new CohereTextVectorizer("embed-english-v3.0", " "));
        Assert.Contains("apiKey must be non-empty.", exception.Message);
    }

    private sealed class RecordingHttpMessageHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
