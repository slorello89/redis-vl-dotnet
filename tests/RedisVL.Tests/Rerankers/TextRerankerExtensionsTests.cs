using RedisVL.Rerankers;

namespace RedisVL.Tests.Rerankers;

public sealed class TextRerankerExtensionsTests
{
    [Fact]
    public async Task RerankAsync_WithStringDocuments_MapsDocumentsIntoRequest()
    {
        var reranker = new RecordingTextReranker();

        var results = await reranker.RerankAsync("redis query", ["alpha", "beta"], topN: 1);

        Assert.NotNull(reranker.Request);
        Assert.Equal("redis query", reranker.Request!.Query);
        Assert.Equal(1, reranker.Request.TopN);
        Assert.Equal(["alpha", "beta"], reranker.Request.Documents.Select(static document => document.Text));
        Assert.Single(results);
        Assert.Equal(0, results[0].Index);
        Assert.Equal(0.99d, results[0].Score);
        Assert.Equal("alpha", results[0].Document.Text);
    }

    [Fact]
    public async Task RerankAsync_WithEmptyInput_ReturnsEmptyCollection()
    {
        var reranker = new RecordingTextReranker();

        var results = await reranker.RerankAsync("redis query", []);

        Assert.Empty(results);
        Assert.Null(reranker.Request);
    }

    [Fact]
    public void RerankRequest_WithBlankQuery_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new RerankRequest(" ", [new RerankDocument("alpha")]));
    }

    [Fact]
    public void RerankRequest_WithInvalidTopN_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new RerankRequest("redis query", [new RerankDocument("alpha")], topN: 0));

        Assert.Equal("topN", exception.ParamName);
    }

    [Fact]
    public void RerankResult_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new RerankResult(-1, 0.5d, new RerankDocument("alpha")));

        Assert.Equal("index", exception.ParamName);
    }

    private sealed class RecordingTextReranker : ITextReranker
    {
        public RerankRequest? Request { get; private set; }

        public Task<IReadOnlyList<RerankResult>> RerankAsync(
            RerankRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;

            return Task.FromResult<IReadOnlyList<RerankResult>>(
            [
                new RerankResult(0, 0.99d, request.Documents[0])
            ]);
        }
    }
}
