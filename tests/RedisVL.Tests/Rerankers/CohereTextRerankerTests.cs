using System.Net;
using System.Net.Http.Headers;
using System.Text;
using RedisVL.Rerankers;
using RedisVL.Rerankers.Cohere;

namespace RedisVL.Tests.Rerankers;

public sealed class CohereTextRerankerTests
{
    [Fact]
    public async Task RerankAsync_WithConfiguredOptions_UsesExpectedRequestShape()
    {
        var handler = new RecordingHttpMessageHandler(
            """{"results":[{"index":1,"relevance_score":0.98},{"index":0,"relevance_score":0.25}]}""");
        using var client = new HttpClient(handler);
        var reranker = new CohereTextReranker(
            "rerank-v4.0-pro",
            "cohere_test_token",
            new CohereRerankerOptions
            {
                MaxTokensPerDocument = 2048,
                Priority = 42,
                ClientName = "redis-vl-dotnet-tests"
            },
            client);
        var request = new RerankRequest(
            "how to reset password",
            [
                new RerankDocument("Reset passwords in Settings", id: "doc-1"),
                new RerankDocument("View invoices in Billing", id: "doc-2")
            ],
            topN: 1);

        var results = await reranker.RerankAsync(request);

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Index);
        Assert.Equal(0.98d, results[0].Score);
        Assert.Same(request.Documents[1], results[0].Document);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.cohere.com/v2/rerank", handler.Request.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "cohere_test_token"), handler.Request.Headers.Authorization);
        Assert.Equal("redis-vl-dotnet-tests", handler.Request.Headers.GetValues("X-Client-Name").Single());
        Assert.Equal(
            """{"model":"rerank-v4.0-pro","query":"how to reset password","documents":["Reset passwords in Settings","View invoices in Billing"],"top_n":1,"max_tokens_per_doc":2048,"priority":42}""",
            handler.RequestBody);
    }

    [Fact]
    public async Task RerankAsync_WithEmptyDocuments_ReturnsEmptyWithoutCallingHttp()
    {
        var handler = new RecordingHttpMessageHandler("""{"results":[]}""");
        using var client = new HttpClient(handler);
        var reranker = new CohereTextReranker("rerank-v4.0-pro", "cohere_test_token", httpClient: client);

        var results = await reranker.RerankAsync(new RerankRequest("redis query", []));

        Assert.Empty(results);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task RerankAsync_WithEndpointOverride_UsesOverrideUri()
    {
        var handler = new RecordingHttpMessageHandler("""{"results":[{"index":0,"relevance_score":0.88}]}""");
        using var client = new HttpClient(handler);
        var reranker = new CohereTextReranker(
            "rerank-v4.0-pro",
            "cohere_test_token",
            new CohereRerankerOptions
            {
                EndpointOverride = "https://example.test/rerank"
            },
            client);

        await reranker.RerankAsync(new RerankRequest("redis query", [new RerankDocument("alpha")]));

        Assert.Equal("https://example.test/rerank", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task RerankAsync_WithErrorResponse_ThrowsInvalidOperationException()
    {
        var handler = new RecordingHttpMessageHandler("""{"message":"invalid token"}""", HttpStatusCode.Unauthorized);
        using var client = new HttpClient(handler);
        var reranker = new CohereTextReranker("rerank-v4.0-pro", "cohere_test_token", httpClient: client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reranker.RerankAsync(new RerankRequest("redis query", [new RerankDocument("alpha")])));

        Assert.Contains("401", exception.Message);
        Assert.Contains("invalid token", exception.Message);
    }

    [Fact]
    public async Task RerankAsync_WithOutOfRangeIndex_ThrowsInvalidOperationException()
    {
        var handler = new RecordingHttpMessageHandler("""{"results":[{"index":2,"relevance_score":0.88}]}""");
        using var client = new HttpClient(handler);
        var reranker = new CohereTextReranker("rerank-v4.0-pro", "cohere_test_token", httpClient: client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reranker.RerankAsync(new RerankRequest("redis query", [new RerankDocument("alpha")])));

        Assert.Equal("Cohere rerank response contained an out-of-range document index.", exception.Message);
    }

    [Fact]
    public void CohereRerankerOptions_WithInvalidPriority_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new CohereRerankerOptions { Priority = 1000 });
        Assert.Contains("Priority must be between 0 and 999.", exception.Message);
    }

    [Fact]
    public void Constructor_WithWhitespaceApiKey_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new CohereTextReranker("rerank-v4.0-pro", " "));
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
