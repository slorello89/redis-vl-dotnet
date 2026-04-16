using System.Net;
using System.Net.Http.Headers;
using System.Text;
using RedisVl.Vectorizers.HuggingFace;

namespace RedisVl.Tests.Vectorizers;

public sealed class HuggingFaceTextVectorizerTests
{
    [Fact]
    public async Task VectorizeAsync_WithSingleInput_UsesConfiguredRequestShape()
    {
        var handler = new RecordingHttpMessageHandler("""[1.5,2.5,3.5]""");
        using var client = new HttpClient(handler);
        var vectorizer = new HuggingFaceTextVectorizer(
            "intfloat/multilingual-e5-large",
            "hf_test_token",
            new HuggingFaceVectorizerOptions
            {
                Normalize = true,
                PromptName = "query",
                Truncate = true,
                TruncationDirection = HuggingFaceTruncationDirection.Left
            },
            client);

        var embedding = await vectorizer.VectorizeAsync("hello world");

        Assert.Equal([1.5f, 2.5f, 3.5f], embedding);
        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://router.huggingface.co/hf-inference/models/intfloat/multilingual-e5-large", handler.Request.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "hf_test_token"), handler.Request.Headers.Authorization);
        Assert.Equal(
            """{"inputs":"hello world","normalize":true,"prompt_name":"query","truncate":true,"truncation_direction":"left"}""",
            handler.RequestBody);
    }

    [Fact]
    public async Task VectorizeAsync_WithMultipleInputs_UsesBatchPayload()
    {
        var handler = new RecordingHttpMessageHandler("""[[1.0,2.0],[3.0,4.0]]""");
        using var client = new HttpClient(handler);
        var vectorizer = new HuggingFaceTextVectorizer("thenlper/gte-large", "hf_test_token", httpClient: client);

        var embeddings = await vectorizer.VectorizeAsync(["alpha", "beta"]);

        Assert.Equal(2, embeddings.Count);
        Assert.Equal([1f, 2f], embeddings[0]);
        Assert.Equal([3f, 4f], embeddings[1]);
        Assert.Equal("""{"inputs":["alpha","beta"]}""", handler.RequestBody);
    }

    [Fact]
    public async Task VectorizeAsync_WithEmptyBatch_ReturnsEmptyWithoutCallingHttp()
    {
        var handler = new RecordingHttpMessageHandler("""[[1.0]]""");
        using var client = new HttpClient(handler);
        var vectorizer = new HuggingFaceTextVectorizer("thenlper/gte-large", "hf_test_token", httpClient: client);

        var embeddings = await vectorizer.VectorizeAsync([]);

        Assert.Empty(embeddings);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task VectorizeAsync_WithEndpointOverride_UsesOverrideUri()
    {
        var handler = new RecordingHttpMessageHandler("""[42.0]""");
        using var client = new HttpClient(handler);
        var vectorizer = new HuggingFaceTextVectorizer(
            "thenlper/gte-large",
            "hf_test_token",
            new HuggingFaceVectorizerOptions
            {
                EndpointOverride = "https://example.test/embeddings"
            },
            client);

        await vectorizer.VectorizeAsync("alpha");

        Assert.Equal("https://example.test/embeddings", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task VectorizeAsync_WithMismatchedBatchResponse_ThrowsInvalidOperationException()
    {
        var handler = new RecordingHttpMessageHandler("""[[1.0,2.0]]""");
        using var client = new HttpClient(handler);
        var vectorizer = new HuggingFaceTextVectorizer("thenlper/gte-large", "hf_test_token", httpClient: client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => vectorizer.VectorizeAsync(["alpha", "beta"]));

        Assert.Equal("Hugging Face embeddings response count did not match the number of requested inputs.", exception.Message);
    }

    [Fact]
    public async Task VectorizeAsync_WithErrorResponse_ThrowsInvalidOperationException()
    {
        var handler = new RecordingHttpMessageHandler("""{"error":"model loading"}""", HttpStatusCode.BadGateway);
        using var client = new HttpClient(handler);
        var vectorizer = new HuggingFaceTextVectorizer("thenlper/gte-large", "hf_test_token", httpClient: client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => vectorizer.VectorizeAsync("alpha"));

        Assert.Contains("502", exception.Message);
        Assert.Contains("model loading", exception.Message);
    }

    [Fact]
    public void Constructor_WithWhitespaceApiKey_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new HuggingFaceTextVectorizer("thenlper/gte-large", " "));
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
