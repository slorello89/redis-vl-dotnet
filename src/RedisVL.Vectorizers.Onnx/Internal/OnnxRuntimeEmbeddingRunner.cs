using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace RedisVL.Vectorizers.Onnx.Internal;

internal sealed class OnnxRuntimeEmbeddingRunner : IOnnxEmbeddingRunner
{
    private static readonly string[] PreferredOutputNames = ["last_hidden_state", "token_embeddings"];

    private readonly InferenceSession _session;
    private readonly string _inputIdsName;
    private readonly string? _attentionMaskName;
    private readonly string? _tokenTypeIdsName;
    private readonly string _outputName;

    public OnnxRuntimeEmbeddingRunner(OnnxVectorizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _session = new InferenceSession(options.ModelPath, CreateSessionOptions(options.SessionOptions));
        _inputIdsName = FindRequiredInputName("input_ids");
        _attentionMaskName = FindOptionalInputName("attention_mask");
        _tokenTypeIdsName = FindOptionalInputName("token_type_ids");
        _outputName = SelectOutputName();
    }

    public float[][] Run(EncodedOnnxInput input)
    {
        var inputValues = new List<NamedOnnxValue>(3)
        {
            CreateInputValue(_inputIdsName, input.InputIds)
        };

        if (_attentionMaskName is not null)
        {
            inputValues.Add(CreateInputValue(_attentionMaskName, input.AttentionMask));
        }

        if (_tokenTypeIdsName is not null)
        {
            inputValues.Add(CreateInputValue(_tokenTypeIdsName, input.TokenTypeIds));
        }

        using var results = _session.Run(inputValues);
        var output = results.Single(result => string.Equals(result.Name, _outputName, StringComparison.Ordinal));

        return ExtractTokenEmbeddings(output);
    }

    public void Dispose() => _session.Dispose();

    private string SelectOutputName()
    {
        foreach (var preferred in PreferredOutputNames)
        {
            var match = _session.OutputMetadata.Keys
                .FirstOrDefault(name => string.Equals(name, preferred, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return _session.OutputMetadata.Keys.First();
    }

    private string FindRequiredInputName(string expectedName) =>
        FindOptionalInputName(expectedName)
        ?? throw new InvalidOperationException($"The ONNX model does not define the required '{expectedName}' input.");

    private string? FindOptionalInputName(string expectedName) =>
        _session.InputMetadata.Keys.FirstOrDefault(name => string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase));

    private NamedOnnxValue CreateInputValue(string inputName, long[] values)
    {
        var inputMetadata = _session.InputMetadata[inputName];
        var dimensions = new[] { 1, values.Length };

        if (inputMetadata.ElementType == typeof(long))
        {
            return NamedOnnxValue.CreateFromTensor(inputName, new DenseTensor<long>(values, dimensions));
        }

        if (inputMetadata.ElementType == typeof(int))
        {
            return NamedOnnxValue.CreateFromTensor(
                inputName,
                new DenseTensor<int>(values.Select(static value => checked((int)value)).ToArray(), dimensions));
        }

        throw new InvalidOperationException(
            $"The ONNX model input '{inputName}' uses unsupported tensor type '{inputMetadata.ElementType}'.");
    }

    private float[][] ExtractTokenEmbeddings(NamedOnnxValue output)
    {
        var elementType = _session.OutputMetadata[_outputName].ElementType;
        if (elementType != typeof(float))
        {
            throw new InvalidOperationException(
                $"The ONNX model output '{output.Name}' uses unsupported tensor type '{elementType}'.");
        }

        var tensor = output.AsTensor<float>();
        var dimensions = tensor.Dimensions;

        return dimensions.Length switch
        {
            // [batch, sequence, hidden] — the standard last_hidden_state shape.
            3 => ReshapeTokenEmbeddings(tensor, dimensions[1], dimensions[2]),
            // [sequence, hidden] — a single-input model that omits the batch dimension.
            2 => ReshapeTokenEmbeddings(tensor, dimensions[0], dimensions[1]),
            // [hidden] — an already-pooled sentence embedding; treat it as one token.
            1 => [tensor.ToArray()],
            _ => throw new InvalidOperationException(
                $"The ONNX model output '{output.Name}' has unsupported rank {dimensions.Length}.")
        };
    }

    private static float[][] ReshapeTokenEmbeddings(Tensor<float> tensor, int sequenceLength, int hiddenSize)
    {
        var flat = tensor.ToArray();
        var tokenEmbeddings = new float[sequenceLength][];

        for (var token = 0; token < sequenceLength; token++)
        {
            var embedding = new float[hiddenSize];
            Array.Copy(flat, token * hiddenSize, embedding, 0, hiddenSize);
            tokenEmbeddings[token] = embedding;
        }

        return tokenEmbeddings;
    }

    private static SessionOptions CreateSessionOptions(OnnxRuntimeSessionOptions? options)
    {
        var sessionOptions = new SessionOptions();
        if (options is null)
        {
            return sessionOptions;
        }

        sessionOptions.GraphOptimizationLevel = options.GraphOptimizationLevel;
        sessionOptions.ExecutionMode = options.ExecutionMode;

        if (options.IntraOpNumThreads is not null)
        {
            sessionOptions.IntraOpNumThreads = options.IntraOpNumThreads.Value;
        }

        if (options.InterOpNumThreads is not null)
        {
            sessionOptions.InterOpNumThreads = options.InterOpNumThreads.Value;
        }

        sessionOptions.EnableCpuMemArena = options.EnableCpuMemoryArena;
        sessionOptions.EnableMemoryPattern = options.EnableMemoryPattern;

        return sessionOptions;
    }
}
