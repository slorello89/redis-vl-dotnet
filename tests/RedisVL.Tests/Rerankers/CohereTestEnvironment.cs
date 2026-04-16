namespace RedisVL.Tests.Rerankers;

internal static class CohereTestEnvironment
{
    public const string ApiKeyEnvironmentVariable = "COHERE_API_KEY";
    public const string ModelEnvironmentVariable = "COHERE_RERANK_MODEL";

    public static string? ApiKey => Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);

    public static string? Model => Environment.GetEnvironmentVariable(ModelEnvironmentVariable);

    public static string? SkipReason =>
        string.IsNullOrWhiteSpace(ApiKey)
            ? $"Set {ApiKeyEnvironmentVariable} to run Cohere smoke tests."
            : null;
}
