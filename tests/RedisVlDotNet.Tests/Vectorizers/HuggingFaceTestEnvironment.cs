namespace RedisVlDotNet.Tests.Vectorizers;

internal static class HuggingFaceTestEnvironment
{
    public static string? ApiKey => Environment.GetEnvironmentVariable("HF_TOKEN");

    public static string? Model => Environment.GetEnvironmentVariable("HF_EMBEDDING_MODEL");
}
