using Microsoft.Extensions.AI;
using RedisVL.Vectorizers.ExtensionsAI;

namespace RedisVL.Tests.Vectorizers;

public sealed class ExtensionsAiTextVectorizerTests
{
    [Fact]
    public async Task VectorizeAsync_WithSingleInput_UsesWrappedGenerator()
    {
        var generator = new RecordingEmbeddingGenerator(
            new GeneratedEmbeddings<Embedding<float>>
            {
                new(new float[] { 1f, 2f, 3f })
            });
        var options = new ExtensionsAiVectorizerOptions
        {
            GenerationOptions = new EmbeddingGenerationOptions()
        };
        var vectorizer = new ExtensionsAiTextVectorizer(generator, options);

        var embedding = await vectorizer.VectorizeAsync("hello world");

        Assert.Equal([1f, 2f, 3f], embedding);
        Assert.Equal(["hello world"], generator.Requests.Single());
        Assert.Same(options.GenerationOptions, generator.Options.Single());
    }

    [Fact]
    public async Task VectorizeAsync_WithBatchInput_UsesWrappedGenerator()
    {
        var generator = new RecordingEmbeddingGenerator(
            new GeneratedEmbeddings<Embedding<float>>
            {
                new(new float[] { 1f }),
                new(new float[] { 2f })
            });
        var vectorizer = new ExtensionsAiTextVectorizer(generator);

        var embeddings = await vectorizer.VectorizeAsync(["alpha", "beta"]);

        Assert.Single(generator.Requests);
        Assert.Equal(["alpha", "beta"], generator.Requests[0]);
        Assert.Equal(2, embeddings.Count);
        Assert.Equal([1f], embeddings[0]);
        Assert.Equal([2f], embeddings[1]);
    }

    [Fact]
    public async Task VectorizeAsync_WithEmptyBatch_ReturnsEmptyWithoutCallingGenerator()
    {
        var generator = new RecordingEmbeddingGenerator();
        var vectorizer = new ExtensionsAiTextVectorizer(generator);

        var embeddings = await vectorizer.VectorizeAsync([]);

        Assert.Empty(embeddings);
        Assert.Empty(generator.Requests);
    }

    [Fact]
    public async Task VectorizeAsync_WithMismatchedBatchCount_ThrowsInvalidOperationException()
    {
        var generator = new RecordingEmbeddingGenerator(
            new GeneratedEmbeddings<Embedding<float>>
            {
                new(new float[] { 1f })
            });
        var vectorizer = new ExtensionsAiTextVectorizer(generator);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            vectorizer.VectorizeAsync(["alpha", "beta"]));

        Assert.Equal(
            "Microsoft.Extensions.AI embeddings response count did not match the number of requested inputs.",
            exception.Message);
    }

    [Fact]
    public void Constructor_WithNullGenerator_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ExtensionsAiTextVectorizer(null!));
    }

    [Fact]
    public async Task VectorizeAsync_WithEmptyInput_ThrowsArgumentException()
    {
        var generator = new RecordingEmbeddingGenerator();
        var vectorizer = new ExtensionsAiTextVectorizer(generator);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => vectorizer.VectorizeAsync(string.Empty));

        Assert.Equal("Embedding input must be non-empty. (Parameter 'input')", exception.Message);
    }

    [Fact]
    public void Dispose_WithDisposeGeneratorEnabled_DisposesWrappedGenerator()
    {
        var generator = new RecordingEmbeddingGenerator();
        var vectorizer = new ExtensionsAiTextVectorizer(
            generator,
            new ExtensionsAiVectorizerOptions
            {
                DisposeGenerator = true
            });

        vectorizer.Dispose();

        Assert.True(generator.Disposed);
    }

    private sealed class RecordingEmbeddingGenerator(
        params GeneratedEmbeddings<Embedding<float>>[] responses) : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly Queue<GeneratedEmbeddings<Embedding<float>>> _responses = new(
            responses.Length == 0
                ? [new GeneratedEmbeddings<Embedding<float>>()]
                : responses);

        public List<string[]> Requests { get; } = [];

        public List<EmbeddingGenerationOptions?> Options { get; } = [];

        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(values.ToArray());
            Options.Add(options);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
