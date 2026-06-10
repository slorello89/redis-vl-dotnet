using RedisVL.Vectorizers;
using RedisVL.Vectorizers.Onnx;
using RedisVL.Vectorizers.Onnx.Internal;

namespace RedisVL.Tests.Vectorizers;

public sealed class OnnxTextVectorizerTests
{
    [Fact]
    public void Constructor_WithMissingModelPath_ThrowsFileNotFoundException()
    {
        var tokenizerPath = CreateTempFile();

        var exception = Assert.Throws<FileNotFoundException>(() => new OnnxTextVectorizer(
            new OnnxVectorizerOptions
            {
                ModelPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.onnx"),
                TokenizerPath = tokenizerPath
            }));

        Assert.Contains("ModelPath does not exist", exception.Message);
        File.Delete(tokenizerPath);
    }

    [Fact]
    public async Task VectorizeAsync_MeanPoolsTokenEmbeddingsWithoutNormalization()
    {
        using var files = TempModelFiles.Create();
        var tokenizer = new StubTokenizer([1, 1]);
        var runner = new StubEmbeddingRunner([[1f, 2f], [3f, 4f]]);
        using var vectorizer = new OnnxTextVectorizer(
            files.CreateOptions(pooling: OnnxPoolingStrategy.Mean, normalize: false),
            tokenizer,
            runner);

        var embedding = await vectorizer.VectorizeAsync("hello world");

        Assert.Equal([2f, 3f], embedding);
        Assert.Equal("hello world", tokenizer.LastText);
    }

    [Fact]
    public async Task VectorizeAsync_MeanPoolingHonorsAttentionMask()
    {
        using var files = TempModelFiles.Create();
        var runner = new StubEmbeddingRunner([[1f, 2f], [10f, 20f]]);
        using var vectorizer = new OnnxTextVectorizer(
            files.CreateOptions(pooling: OnnxPoolingStrategy.Mean, normalize: false),
            new StubTokenizer([1, 0]),
            runner);

        var embedding = await vectorizer.VectorizeAsync("masked");

        Assert.Equal([1f, 2f], embedding);
    }

    [Fact]
    public async Task VectorizeAsync_WithNormalization_ReturnsUnitVector()
    {
        using var files = TempModelFiles.Create();
        var runner = new StubEmbeddingRunner([[3f, 4f]]);
        using var vectorizer = new OnnxTextVectorizer(
            files.CreateOptions(pooling: OnnxPoolingStrategy.Mean, normalize: true),
            new StubTokenizer([1]),
            runner);

        var embedding = await vectorizer.VectorizeAsync("normalize");

        Assert.Equal(0.6f, embedding[0], precision: 5);
        Assert.Equal(0.8f, embedding[1], precision: 5);
        var magnitude = Math.Sqrt((embedding[0] * embedding[0]) + (embedding[1] * embedding[1]));
        Assert.Equal(1d, magnitude, precision: 5);
    }

    [Fact]
    public async Task VectorizeAsync_WithClsPooling_UsesLeadingToken()
    {
        using var files = TempModelFiles.Create();
        var runner = new StubEmbeddingRunner([[7f, 8f], [100f, 200f]]);
        using var vectorizer = new OnnxTextVectorizer(
            files.CreateOptions(pooling: OnnxPoolingStrategy.Cls, normalize: false),
            new StubTokenizer([1, 1]),
            runner);

        var embedding = await vectorizer.VectorizeAsync("cls");

        Assert.Equal([7f, 8f], embedding);
    }

    [Fact]
    public async Task VectorizeAsync_Batch_ReturnsOneEmbeddingPerInput()
    {
        using var files = TempModelFiles.Create();
        var runner = new StubEmbeddingRunner([[1f, 1f]], [[2f, 2f]]);
        using var vectorizer = new OnnxTextVectorizer(
            files.CreateOptions(pooling: OnnxPoolingStrategy.Mean, normalize: false),
            new StubTokenizer([1]),
            runner);

        var embeddings = await vectorizer.VectorizeAsync(["first", "second"]);

        Assert.Equal(2, embeddings.Count);
        Assert.Equal([1f, 1f], embeddings[0]);
        Assert.Equal([2f, 2f], embeddings[1]);
    }

    [Fact]
    public async Task VectorizeAsync_WithEmptyBatch_ReturnsEmptyWithoutInference()
    {
        using var files = TempModelFiles.Create();
        var runner = new StubEmbeddingRunner();
        using var vectorizer = new OnnxTextVectorizer(
            files.CreateOptions(),
            new StubTokenizer([1]),
            runner);

        var embeddings = await vectorizer.VectorizeAsync(Array.Empty<string>());

        Assert.Empty(embeddings);
        Assert.Equal(0, runner.RunCount);
    }

    [Fact]
    public async Task VectorizeManyAsync_DispatchesToBatchImplementation()
    {
        using var files = TempModelFiles.Create();
        var runner = new StubEmbeddingRunner([[5f]], [[6f]]);
        using var vectorizer = new OnnxTextVectorizer(
            files.CreateOptions(pooling: OnnxPoolingStrategy.Mean, normalize: false),
            new StubTokenizer([1]),
            runner);

        ITextVectorizer abstraction = vectorizer;
        var embeddings = await abstraction.VectorizeManyAsync(["a", "b"]);

        Assert.Equal(2, embeddings.Count);
        Assert.Equal([5f], embeddings[0]);
        Assert.Equal([6f], embeddings[1]);
    }

    [Fact]
    public async Task VectorizeAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        using var files = TempModelFiles.Create();
        var vectorizer = new OnnxTextVectorizer(
            files.CreateOptions(),
            new StubTokenizer([1]),
            new StubEmbeddingRunner([[1f]]));

        vectorizer.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => vectorizer.VectorizeAsync("disposed"));
    }

    [Fact]
    public void OnnxVectorizerOptions_WithInvalidMaxSequenceLength_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new OnnxVectorizerOptions
        {
            ModelPath = "model.onnx",
            TokenizerPath = "tokenizer.json",
            MaxSequenceLength = 0
        });

        Assert.Contains("MaxSequenceLength must be greater than zero.", exception.Message);
    }

    private sealed class StubTokenizer(long[] attentionMask) : IOnnxTextTokenizer
    {
        public string? LastText { get; private set; }

        public EncodedOnnxInput Encode(string text, int maxSequenceLength)
        {
            Assert.False(string.IsNullOrEmpty(text));
            Assert.True(maxSequenceLength > 0);
            LastText = text;

            var inputIds = new long[attentionMask.Length];
            var tokenTypeIds = new long[attentionMask.Length];
            return new EncodedOnnxInput(inputIds, attentionMask, tokenTypeIds);
        }
    }

    private sealed class StubEmbeddingRunner(params float[][][] perCallTokenEmbeddings) : IOnnxEmbeddingRunner
    {
        private readonly Queue<float[][]> _outputs = new(perCallTokenEmbeddings);

        public int RunCount { get; private set; }

        public float[][] Run(EncodedOnnxInput input)
        {
            RunCount++;
            return _outputs.Count > 0 ? _outputs.Dequeue() : [[0f]];
        }

        public void Dispose()
        {
        }
    }

    private sealed class TempModelFiles : IDisposable
    {
        private TempModelFiles(string modelPath, string tokenizerPath)
        {
            ModelPath = modelPath;
            TokenizerPath = tokenizerPath;
        }

        public string ModelPath { get; }

        public string TokenizerPath { get; }

        public static TempModelFiles Create()
        {
            var modelPath = CreateTempFile(".onnx");
            var tokenizerPath = CreateTempFile(".json");
            return new TempModelFiles(modelPath, tokenizerPath);
        }

        public OnnxVectorizerOptions CreateOptions(
            OnnxPoolingStrategy pooling = OnnxPoolingStrategy.Mean,
            bool normalize = true) =>
            new()
            {
                ModelPath = ModelPath,
                TokenizerPath = TokenizerPath,
                Pooling = pooling,
                Normalize = normalize
            };

        public void Dispose()
        {
            File.Delete(ModelPath);
            File.Delete(TokenizerPath);
        }
    }

    private static string CreateTempFile(string extension = ".tmp")
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, "fixture");
        return path;
    }
}
