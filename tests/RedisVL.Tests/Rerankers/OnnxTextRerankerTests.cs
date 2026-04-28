using RedisVL.Rerankers;
using RedisVL.Rerankers.Onnx;
using RedisVL.Rerankers.Onnx.Internal;

namespace RedisVL.Tests.Rerankers;

public sealed class OnnxTextRerankerTests
{
    [Fact]
    public void Constructor_WithMissingModelPath_ThrowsFileNotFoundException()
    {
        var tokenizerPath = CreateTempFile();

        var exception = Assert.Throws<FileNotFoundException>(() => new OnnxTextReranker(
            new OnnxRerankerOptions
            {
                ModelPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.onnx"),
                TokenizerPath = tokenizerPath
            }));

        Assert.Contains("ModelPath does not exist", exception.Message);
        File.Delete(tokenizerPath);
    }

    [Fact]
    public async Task RerankAsync_WithEmptyDocuments_ReturnsEmptyWithoutScoring()
    {
        using var files = TempModelFiles.Create();
        using var inferenceRunner = new RecordingInferenceRunner();
        var reranker = new OnnxTextReranker(
            files.CreateOptions(),
            new RecordingTokenizer(),
            inferenceRunner);

        var results = await reranker.RerankAsync(new RerankRequest("redis query", []));

        Assert.Empty(results);
        Assert.Empty(inferenceRunner.RecordedInputs);
    }

    [Fact]
    public async Task RerankAsync_OrdersResultsDescendingAndPreservesMetadata()
    {
        using var files = TempModelFiles.Create();
        using var inferenceRunner = new RecordingInferenceRunner(0.30d, 0.95d, 0.60d);
        var reranker = new OnnxTextReranker(
            files.CreateOptions(),
            new RecordingTokenizer(),
            inferenceRunner);
        var documents = new[]
        {
            new RerankDocument("alpha", id: "doc-1", metadata: new { Value = 1 }),
            new RerankDocument("beta", id: "doc-2", metadata: new { Value = 2 }),
            new RerankDocument("gamma", id: "doc-3", metadata: new { Value = 3 })
        };

        var results = await reranker.RerankAsync(new RerankRequest("best match", documents));

        Assert.Equal(["doc-2", "doc-3", "doc-1"], results.Select(result => result.Document.Id));
        Assert.Equal([0.95d, 0.60d, 0.30d], results.Select(result => result.Score));
        Assert.Same(documents[1], results[0].Document);
        Assert.Equal(1, results[0].Index);
    }

    [Fact]
    public async Task RerankAsync_WithTopN_ReturnsOnlyTopMatches()
    {
        using var files = TempModelFiles.Create();
        using var inferenceRunner = new RecordingInferenceRunner(0.10d, 0.90d, 0.40d);
        var reranker = new OnnxTextReranker(
            files.CreateOptions(),
            new RecordingTokenizer(),
            inferenceRunner);

        var results = await reranker.RerankAsync(
            new RerankRequest(
                "best match",
                [
                    new RerankDocument("alpha", id: "doc-1"),
                    new RerankDocument("beta", id: "doc-2"),
                    new RerankDocument("gamma", id: "doc-3")
                ],
                topN: 2));

        Assert.Equal(2, results.Count);
        Assert.Equal(["doc-2", "doc-3"], results.Select(result => result.Document.Id));
    }

    [Fact]
    public async Task RerankAsync_WithScoreThreshold_FiltersLowScoringDocuments()
    {
        using var files = TempModelFiles.Create();
        using var inferenceRunner = new RecordingInferenceRunner(0.49d, 0.50d, 0.91d);
        var reranker = new OnnxTextReranker(
            files.CreateOptions(scoreThreshold: 0.50d),
            new RecordingTokenizer(),
            inferenceRunner);

        var results = await reranker.RerankAsync(
            new RerankRequest(
                "best match",
                [
                    new RerankDocument("alpha", id: "doc-1"),
                    new RerankDocument("beta", id: "doc-2"),
                    new RerankDocument("gamma", id: "doc-3")
                ]));

        Assert.Equal(["doc-3", "doc-2"], results.Select(result => result.Document.Id));
    }

    [Fact]
    public void OnnxRuntimeSessionOptions_WithInvalidThreadCount_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new OnnxRuntimeSessionOptions
        {
            IntraOpNumThreads = 0
        });

        Assert.Contains("IntraOpNumThreads must be greater than zero.", exception.Message);
    }

    private sealed class RecordingTokenizer : IOnnxPairTokenizer
    {
        public EncodedOnnxInput Encode(string query, string document, int maxSequenceLength)
        {
            Assert.False(string.IsNullOrWhiteSpace(query));
            Assert.False(string.IsNullOrWhiteSpace(document));
            Assert.True(maxSequenceLength > 0);

            return new EncodedOnnxInput([101, 102], [1, 1], [0, 0]);
        }
    }

    private sealed class RecordingInferenceRunner(params double[] scores) : IOnnxInferenceRunner
    {
        private readonly Queue<double> _scores = new(scores);

        public List<EncodedOnnxInput> RecordedInputs { get; } = [];

        public double Score(EncodedOnnxInput input)
        {
            RecordedInputs.Add(input);
            return _scores.Count > 0 ? _scores.Dequeue() : 0d;
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

        public OnnxRerankerOptions CreateOptions(double? scoreThreshold = null) =>
            new()
            {
                ModelPath = ModelPath,
                TokenizerPath = TokenizerPath,
                ScoreThreshold = scoreThreshold
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
