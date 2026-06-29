namespace RedisVL.Tests.Rerankers;

internal static class VoyageAiTestEnvironment
{
    public const string ApiKeyEnvironmentVariable = "VOYAGE_API_KEY";
    public const string ModelEnvironmentVariable = "VOYAGE_RERANK_MODEL";

    public static string? ApiKey => Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);

    public static string? Model => Environment.GetEnvironmentVariable(ModelEnvironmentVariable);

    public static string? SkipReason =>
        string.IsNullOrWhiteSpace(ApiKey)
            ? $"Set {ApiKeyEnvironmentVariable} to run Voyage AI smoke tests."
            : null;
}
