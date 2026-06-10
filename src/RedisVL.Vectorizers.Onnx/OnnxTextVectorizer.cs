using RedisVL.Vectorizers;
using RedisVL.Vectorizers.Onnx.Internal;

namespace RedisVL.Vectorizers.Onnx;

/// <summary>
/// Generates sentence embeddings locally with a BERT-style ONNX SentenceTransformers model,
/// without calling out to a remote service or requiring an API key.
/// </summary>
public sealed class OnnxTextVectorizer : IBatchTextVectorizer, IDisposable
{
    private readonly IOnnxTextTokenizer _tokenizer;
    private readonly IOnnxEmbeddingRunner _embeddingRunner;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnnxTextVectorizer" /> class.
    /// </summary>
    /// <param name="options">The local model and runtime configuration.</param>
    public OnnxTextVectorizer(OnnxVectorizerOptions options)
        : this(
            ValidateOptions(options),
            new BertTokenizerJsonTextTokenizer(options.TokenizerPath),
            new OnnxRuntimeEmbeddingRunner(options))
    {
    }

    internal OnnxTextVectorizer(
        OnnxVectorizerOptions options,
        IOnnxTextTokenizer tokenizer,
        IOnnxEmbeddingRunner embeddingRunner)
    {
        Options = ValidateOptions(options);
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        _embeddingRunner = embeddingRunner ?? throw new ArgumentNullException(nameof(embeddingRunner));
    }

    /// <summary>
    /// Gets the vectorizer configuration.
    /// </summary>
    public OnnxVectorizerOptions Options { get; }

    /// <inheritdoc />
    public Task<float[]> VectorizeAsync(string input, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(input);

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Embed(input));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<float[]>> VectorizeAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(inputs);

        if (inputs.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<float[]>>([]);
        }

        var embeddings = new float[inputs.Count][];
        for (var index = 0; index < inputs.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var input = inputs[index];
            if (input is null)
            {
                throw new ArgumentException($"Embedding input at index {index} must not be null.", nameof(inputs));
            }

            embeddings[index] = Embed(input);
        }

        return Task.FromResult<IReadOnlyList<float[]>>(embeddings);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _embeddingRunner.Dispose();
        _disposed = true;
    }

    private float[] Embed(string input)
    {
        var encoded = _tokenizer.Encode(input, Options.MaxSequenceLength);
        var tokenEmbeddings = _embeddingRunner.Run(encoded);
        return EmbeddingPooler.Pool(tokenEmbeddings, encoded.AttentionMask, Options.Pooling, Options.Normalize);
    }

    private static OnnxVectorizerOptions ValidateOptions(OnnxVectorizerOptions options)
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
