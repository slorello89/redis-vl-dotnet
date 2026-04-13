using RedisVlDotNet.Vectorizers;

namespace RedisVlDotNet.Tests.Vectorizers;

public sealed class TextVectorizerExtensionsTests
{
    [Fact]
    public async Task VectorizeManyAsync_WithBatchVectorizer_UsesBatchImplementation()
    {
        var vectorizer = new RecordingBatchVectorizer();

        var embeddings = await vectorizer.VectorizeManyAsync(["alpha", "beta"]);

        Assert.Equal(1, vectorizer.BatchCallCount);
        Assert.Equal(0, vectorizer.SingleCallCount);
        Assert.Equal(2, embeddings.Count);
        Assert.Equal([5f], embeddings[0]);
        Assert.Equal([4f], embeddings[1]);
    }

    [Fact]
    public async Task VectorizeManyAsync_WithSingleVectorizer_FallsBackToPerInputExecution()
    {
        var vectorizer = new RecordingSingleVectorizer();

        var embeddings = await vectorizer.VectorizeManyAsync(["alpha", "beta"]);

        Assert.Equal(["alpha", "beta"], vectorizer.Inputs);
        Assert.Equal(2, embeddings.Count);
        Assert.Equal([5f], embeddings[0]);
        Assert.Equal([4f], embeddings[1]);
    }

    [Fact]
    public async Task VectorizeManyAsync_WithEmptyInput_ReturnsEmptyCollection()
    {
        var vectorizer = new RecordingSingleVectorizer();

        var embeddings = await vectorizer.VectorizeManyAsync([]);

        Assert.Empty(embeddings);
        Assert.Empty(vectorizer.Inputs);
    }

    private sealed class RecordingSingleVectorizer : ITextVectorizer
    {
        public List<string> Inputs { get; } = [];

        public Task<float[]> VectorizeAsync(string input, CancellationToken cancellationToken = default)
        {
            Inputs.Add(input);
            return Task.FromResult(new[] { (float)input.Length });
        }
    }

    private sealed class RecordingBatchVectorizer : IBatchTextVectorizer
    {
        public int BatchCallCount { get; private set; }

        public int SingleCallCount { get; private set; }

        public Task<float[]> VectorizeAsync(string input, CancellationToken cancellationToken = default)
        {
            SingleCallCount++;
            return Task.FromResult(new[] { (float)input.Length });
        }

        public Task<IReadOnlyList<float[]>> VectorizeAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken = default)
        {
            BatchCallCount++;
            return Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(static input => new[] { (float)input.Length }).ToArray());
        }
    }
}
