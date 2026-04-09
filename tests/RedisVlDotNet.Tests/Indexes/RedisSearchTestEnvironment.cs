namespace RedisVlDotNet.Tests.Indexes;

internal static class RedisSearchTestEnvironment
{
    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL");

    public static string? SkipReason =>
        string.IsNullOrWhiteSpace(ConnectionString)
            ? "Set REDIS_VL_REDIS_URL to run Redis Stack integration tests."
            : null;
}
