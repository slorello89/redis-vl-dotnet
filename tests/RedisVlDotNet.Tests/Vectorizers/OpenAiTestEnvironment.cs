namespace RedisVlDotNet.Tests.Vectorizers;

internal static class OpenAiTestEnvironment
{
    public const string ApiKeyEnvironmentVariable = "OPENAI_API_KEY";
    public const string ModelEnvironmentVariable = "OPENAI_EMBEDDING_MODEL";

    public static string? ApiKey => Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);

    public static string? Model => Environment.GetEnvironmentVariable(ModelEnvironmentVariable);

    public static string? SkipReason =>
        string.IsNullOrWhiteSpace(ApiKey)
            ? $"Set {ApiKeyEnvironmentVariable} to run OpenAI smoke tests."
            : null;
}
