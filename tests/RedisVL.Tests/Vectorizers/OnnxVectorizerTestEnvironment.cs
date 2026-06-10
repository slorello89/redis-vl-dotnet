namespace RedisVL.Tests.Vectorizers;

internal static class OnnxVectorizerTestEnvironment
{
    public static string? ModelPath => Environment.GetEnvironmentVariable("ONNX_VECTORIZER_MODEL_PATH");

    public static string? TokenizerPath => Environment.GetEnvironmentVariable("ONNX_VECTORIZER_TOKENIZER_PATH");

    public static string? SkipReason =>
        string.IsNullOrWhiteSpace(ModelPath) || string.IsNullOrWhiteSpace(TokenizerPath)
            ? "Set ONNX_VECTORIZER_MODEL_PATH and ONNX_VECTORIZER_TOKENIZER_PATH to run ONNX vectorizer smoke tests."
            : null;
}
