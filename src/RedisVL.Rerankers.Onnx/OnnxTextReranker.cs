using RedisVL.Rerankers;
using RedisVL.Rerankers.Onnx.Internal;

namespace RedisVL.Rerankers.Onnx;

/// <summary>
/// Reranks an existing candidate set locally with a BERT-style ONNX cross-encoder model.
/// </summary>
public sealed class OnnxTextReranker : ITextReranker, IDisposable
{
    private readonly IOnnxPairTokenizer _tokenizer;
    private readonly IOnnxInferenceRunner _inferenceRunner;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnnxTextReranker" /> class.
    /// </summary>
    /// <param name="options">The local model and runtime configuration.</param>
    public OnnxTextReranker(OnnxRerankerOptions options)
        : this(
            ValidateOptions(options),
            new BertTokenizerJsonPairTokenizer(options.TokenizerPath),
            new OnnxRuntimeInferenceRunner(options))
    {
    }

    internal OnnxTextReranker(
        OnnxRerankerOptions options,
        IOnnxPairTokenizer tokenizer,
        IOnnxInferenceRunner inferenceRunner)
    {
        Options = ValidateOptions(options);
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        _inferenceRunner = inferenceRunner ?? throw new ArgumentNullException(nameof(inferenceRunner));
    }

    /// <summary>
    /// Gets the reranker configuration.
    /// </summary>
    public OnnxRerankerOptions Options { get; }

    /// <inheritdoc />
    public Task<IReadOnlyList<RerankResult>> RerankAsync(
        RerankRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Documents.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<RerankResult>>([]);
        }

        var results = new List<RerankResult>(request.Documents.Count);
        for (var index = 0; index < request.Documents.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var document = request.Documents[index];
            var encodedInput = _tokenizer.Encode(request.Query, document.Text, Options.MaxSequenceLength);
            var score = _inferenceRunner.Score(encodedInput);

            if (Options.ScoreThreshold is not null && score < Options.ScoreThreshold.Value)
            {
                continue;
            }

            results.Add(new RerankResult(index, score, document));
        }

        results.Sort(static (left, right) =>
        {
            var scoreComparison = right.Score.CompareTo(left.Score);
            return scoreComparison != 0 ? scoreComparison : left.Index.CompareTo(right.Index);
        });

        if (request.TopN is not null && results.Count > request.TopN.Value)
        {
            results.RemoveRange(request.TopN.Value, results.Count - request.TopN.Value);
        }

        return Task.FromResult<IReadOnlyList<RerankResult>>(results);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _inferenceRunner.Dispose();
        _disposed = true;
    }

    private static OnnxRerankerOptions ValidateOptions(OnnxRerankerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!File.Exists(options.ModelPath))
        {
            throw new FileNotFoundException($"ModelPath does not exist: {options.ModelPath}", options.ModelPath);
        }

        if (!File.Exists(options.TokenizerPath))
        {
            throw new FileNotFoundException($"TokenizerPath does not exist: {options.TokenizerPath}", options.TokenizerPath);
        }

        return options;
    }
}
