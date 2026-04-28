using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace RedisVL.Rerankers.Onnx.Internal;

internal sealed class OnnxRuntimeInferenceRunner : IOnnxInferenceRunner
{
    private readonly InferenceSession _session;
    private readonly string _inputIdsName;
    private readonly string? _attentionMaskName;
    private readonly string? _tokenTypeIdsName;
    private readonly string _outputName;

    public OnnxRuntimeInferenceRunner(OnnxRerankerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _session = new InferenceSession(options.ModelPath, CreateSessionOptions(options.SessionOptions));
        _inputIdsName = FindRequiredInputName("input_ids");
        _attentionMaskName = FindOptionalInputName("attention_mask");
        _tokenTypeIdsName = FindOptionalInputName("token_type_ids");
        _outputName = _session.OutputMetadata.Keys.First();
    }

    public double Score(EncodedOnnxInput input)
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

        return ExtractScalar(output, _session.OutputMetadata[_outputName].ElementType);
    }

    public void Dispose() => _session.Dispose();

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

    private static double ExtractScalar(NamedOnnxValue output, Type elementType)
    {
        if (elementType == typeof(float))
        {
            return ExtractSingleScalar(output.AsEnumerable<float>().Select(static value => (double)value).ToArray());
        }

        if (elementType == typeof(double))
        {
            return ExtractSingleScalar(output.AsEnumerable<double>().ToArray());
        }

        if (elementType == typeof(int))
        {
            return ExtractSingleScalar(output.AsEnumerable<int>().Select(static value => (double)value).ToArray());
        }

        if (elementType == typeof(long))
        {
            return ExtractSingleScalar(output.AsEnumerable<long>().Select(static value => (double)value).ToArray());
        }

        throw new InvalidOperationException(
            $"The ONNX model output '{output.Name}' uses unsupported tensor type '{elementType}'.");
    }

    private static double ExtractSingleScalar(IReadOnlyList<double> values)
    {
        if (values.Count != 1)
        {
            throw new InvalidOperationException(
                $"The ONNX model output must contain a single scalar score, but it returned {values.Count} values.");
        }

        return values[0];
    }
}
