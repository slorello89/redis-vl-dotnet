using System.ClientModel;
using System.ClientModel.Primitives;
using OpenAI.Embeddings;
using RedisVlDotNet.Vectorizers.OpenAI;

namespace RedisVlDotNet.Tests.Vectorizers;

public sealed class OpenAiTextVectorizerTests
{
    [Fact]
    public async Task VectorizeAsync_WithSingleInput_UsesConfiguredOptions()
    {
        var client = new RecordingEmbeddingClient();
        var vectorizer = new OpenAiTextVectorizer(
            client,
            new OpenAiVectorizerOptions
            {
                Dimensions = 8,
                EndUserId = "user-123"
            });

        var embedding = await vectorizer.VectorizeAsync("hello world");

        Assert.Equal("hello world", client.SingleInput);
        Assert.NotNull(client.LastOptions);
        Assert.Equal(8, client.LastOptions!.Dimensions);
        Assert.Equal("user-123", client.LastOptions.EndUserId);
        Assert.Equal([1f, 2f, 3f], embedding);
    }

    [Fact]
    public async Task VectorizeAsync_WithMultipleInputs_UsesBatchEmbeddingRequest()
    {
        var client = new RecordingEmbeddingClient();
        var vectorizer = new OpenAiTextVectorizer(client);

        var embeddings = await vectorizer.VectorizeAsync(["alpha", "beta"]);

        Assert.Equal(["alpha", "beta"], client.BatchInputs);
        Assert.Equal(2, embeddings.Count);
        Assert.Equal([10f], embeddings[0]);
        Assert.Equal([20f], embeddings[1]);
    }

    [Fact]
    public async Task VectorizeAsync_WithEmptyBatch_ReturnsEmptyWithoutCallingClient()
    {
        var client = new RecordingEmbeddingClient();
        var vectorizer = new OpenAiTextVectorizer(client);

        var embeddings = await vectorizer.VectorizeAsync([]);

        Assert.Empty(embeddings);
        Assert.Null(client.BatchInputs);
    }

    [Fact]
    public async Task VectorizeAsync_WithMismatchedBatchResponse_ThrowsInvalidOperationException()
    {
        var client = new RecordingEmbeddingClient
        {
            BatchResponse = CreateBatchResult([[42f]])
        };

        var vectorizer = new OpenAiTextVectorizer(client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => vectorizer.VectorizeAsync(["alpha", "beta"]));
        Assert.Equal("OpenAI embeddings response count did not match the number of requested inputs.", exception.Message);
    }

    [Fact]
    public async Task VectorizeAsync_WhenClientThrows_PropagatesException()
    {
        var client = new ThrowingEmbeddingClient(new InvalidOperationException("boom"));
        var vectorizer = new OpenAiTextVectorizer(client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => vectorizer.VectorizeAsync("hello"));
        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public void OpenAiVectorizerOptions_WithInvalidDimensions_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new OpenAiVectorizerOptions { Dimensions = 0 });
        Assert.Contains("Dimensions must be greater than zero.", exception.Message);
    }

    private static ClientResult<OpenAIEmbedding> CreateSingleResult(float[] values)
    {
        return ClientResult.FromValue(
            OpenAIEmbeddingsModelFactory.OpenAIEmbedding(0, values),
            new TestPipelineResponse());
    }

    private static ClientResult<OpenAIEmbeddingCollection> CreateBatchResult(float[][] values)
    {
        return ClientResult.FromValue(
            OpenAIEmbeddingsModelFactory.OpenAIEmbeddingCollection(
                values.Select(static (embedding, index) => OpenAIEmbeddingsModelFactory.OpenAIEmbedding(index, embedding)),
                "text-embedding-3-small",
                OpenAIEmbeddingsModelFactory.EmbeddingTokenUsage(2, 2)),
            new TestPipelineResponse());
    }

    private sealed class RecordingEmbeddingClient : EmbeddingClient
    {
        public RecordingEmbeddingClient()
            : base("text-embedding-3-small", "test-api-key")
        {
        }

        public string? SingleInput { get; private set; }

        public IReadOnlyList<string>? BatchInputs { get; private set; }

        public EmbeddingGenerationOptions? LastOptions { get; private set; }

        public ClientResult<OpenAIEmbedding> SingleResponse { get; init; } = CreateSingleResult([1f, 2f, 3f]);

        public ClientResult<OpenAIEmbeddingCollection> BatchResponse { get; init; } = CreateBatchResult([[10f], [20f]]);

        public override Task<ClientResult<OpenAIEmbedding>> GenerateEmbeddingAsync(
            string input,
            EmbeddingGenerationOptions options = null!,
            CancellationToken cancellationToken = default)
        {
            SingleInput = input;
            LastOptions = options;
            return Task.FromResult(SingleResponse);
        }

        public override Task<ClientResult<OpenAIEmbeddingCollection>> GenerateEmbeddingsAsync(
            IEnumerable<string> inputs,
            EmbeddingGenerationOptions options = null!,
            CancellationToken cancellationToken = default)
        {
            BatchInputs = inputs.ToArray();
            LastOptions = options;
            return Task.FromResult(BatchResponse);
        }
    }

    private sealed class ThrowingEmbeddingClient(Exception exception) : EmbeddingClient("text-embedding-3-small", "test-api-key")
    {
        public override Task<ClientResult<OpenAIEmbedding>> GenerateEmbeddingAsync(
            string input,
            EmbeddingGenerationOptions options = null!,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ClientResult<OpenAIEmbedding>>(exception);
    }

    private sealed class TestPipelineResponse : PipelineResponse
    {
        private Stream? _contentStream;

        public override int Status => 200;

        public override string ReasonPhrase => "OK";

        public override BinaryData Content => BinaryData.FromString(string.Empty);

        public override Stream? ContentStream
        {
            get => _contentStream;
            set => _contentStream = value;
        }

        protected override PipelineResponseHeaders HeadersCore => throw new NotSupportedException();

        public override void Dispose()
        {
        }

        public override BinaryData BufferContent(CancellationToken cancellationToken = default) => BinaryData.FromString(string.Empty);

        public override ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(BinaryData.FromString(string.Empty));
    }
}
