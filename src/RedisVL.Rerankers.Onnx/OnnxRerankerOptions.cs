using Microsoft.ML.OnnxRuntime;

namespace RedisVL.Rerankers.Onnx;

/// <summary>
/// Configures the local ONNX reranker model assets and runtime behavior.
/// </summary>
public sealed class OnnxRerankerOptions
{
    private int _maxSequenceLength = 512;
    private string? _modelPath;
    private string? _tokenizerPath;

    /// <summary>
    /// Gets the local path to the ONNX model file.
    /// </summary>
    public required string ModelPath
    {
        get => _modelPath ?? throw new InvalidOperationException("ModelPath must be configured.");
        init => _modelPath = ValidateRequired(value, nameof(ModelPath));
    }

    /// <summary>
    /// Gets the local path to the tokenizer.json file.
    /// </summary>
    public required string TokenizerPath
    {
        get => _tokenizerPath ?? throw new InvalidOperationException("TokenizerPath must be configured.");
        init => _tokenizerPath = ValidateRequired(value, nameof(TokenizerPath));
    }

    /// <summary>
    /// Gets the maximum sequence length used for pair tokenization.
    /// </summary>
    public int MaxSequenceLength
    {
        get => _maxSequenceLength;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "MaxSequenceLength must be greater than zero.");
            }

            _maxSequenceLength = value;
        }
    }

    /// <summary>
    /// Gets the optional score threshold applied after inference.
    /// </summary>
    public double? ScoreThreshold { get; init; }

    /// <summary>
    /// Gets the optional ONNX Runtime session configuration.
    /// </summary>
    public OnnxRuntimeSessionOptions? SessionOptions { get; init; }

    private static string ValidateRequired(string value, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} must be non-empty.", paramName);
        }

        return value;
    }
}

/// <summary>
/// Configures the ONNX Runtime session used by <see cref="OnnxTextReranker" />.
/// </summary>
public sealed class OnnxRuntimeSessionOptions
{
    private int? _interOpNumThreads;
    private int? _intraOpNumThreads;

    /// <summary>
    /// Gets the graph optimization level for the ONNX session.
    /// </summary>
    public GraphOptimizationLevel GraphOptimizationLevel { get; init; } = GraphOptimizationLevel.ORT_ENABLE_ALL;

    /// <summary>
    /// Gets the execution mode for the ONNX session.
    /// </summary>
    public ExecutionMode ExecutionMode { get; init; } = ExecutionMode.ORT_SEQUENTIAL;

    /// <summary>
    /// Gets whether the CPU memory arena remains enabled.
    /// </summary>
    public bool EnableCpuMemoryArena { get; init; } = true;

    /// <summary>
    /// Gets whether memory pattern optimization remains enabled.
    /// </summary>
    public bool EnableMemoryPattern { get; init; } = true;

    /// <summary>
     /// Gets the optional intra-op thread count.
     /// </summary>
    public int? IntraOpNumThreads
    {
        get => _intraOpNumThreads;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "IntraOpNumThreads must be greater than zero.");
            }

            _intraOpNumThreads = value;
        }
    }

    /// <summary>
    /// Gets the optional inter-op thread count.
    /// </summary>
    public int? InterOpNumThreads
    {
        get => _interOpNumThreads;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "InterOpNumThreads must be greater than zero.");
            }

            _interOpNumThreads = value;
        }
    }
}
