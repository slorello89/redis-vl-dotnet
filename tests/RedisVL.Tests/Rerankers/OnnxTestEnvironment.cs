namespace RedisVL.Tests.Rerankers;

internal static class OnnxTestEnvironment
{
    public static string? ModelPath => Environment.GetEnvironmentVariable("ONNX_RERANKER_MODEL_PATH");

    public static string? TokenizerPath => Environment.GetEnvironmentVariable("ONNX_RERANKER_TOKENIZER_PATH");

    public static string? SkipReason =>
        string.IsNullOrWhiteSpace(ModelPath) || string.IsNullOrWhiteSpace(TokenizerPath)
            ? "Set ONNX_RERANKER_MODEL_PATH and ONNX_RERANKER_TOKENIZER_PATH to run ONNX smoke tests."
            : null;
}
