using StackExchange.Redis;

namespace RedisVl.Tests.Indexes;

internal static class RedisSentinelTestEnvironment
{
    public const string SentinelNodesEnvironmentVariable = "REDIS_VL_REDIS_SENTINEL_NODES";
    public const string ServiceNameEnvironmentVariable = "REDIS_VL_REDIS_SENTINEL_SERVICE_NAME";
    public const string UserEnvironmentVariable = "REDIS_VL_REDIS_USER";
    public const string PasswordEnvironmentVariable = "REDIS_VL_REDIS_PASSWORD";
    public const string SslEnvironmentVariable = "REDIS_VL_REDIS_SSL";

    public static string? SentinelNodes =>
        Environment.GetEnvironmentVariable(SentinelNodesEnvironmentVariable);

    public static string? ServiceName =>
        Environment.GetEnvironmentVariable(ServiceNameEnvironmentVariable);

    public static string? SkipReason =>
        string.IsNullOrWhiteSpace(SentinelNodes)
            ? $"Set {SentinelNodesEnvironmentVariable} to run Redis Sentinel integration tests."
            : string.IsNullOrWhiteSpace(ServiceName)
                ? $"Set {ServiceNameEnvironmentVariable} to run Redis Sentinel integration tests."
                : null;

    public static Task<IConnectionMultiplexer> ConnectPrimaryAsync(CancellationToken cancellationToken = default) =>
        RedisConnectionFactory.ConnectSentinelPrimaryAsync(
            SentinelNodes!,
            ServiceName!,
            options =>
            {
                var user = Environment.GetEnvironmentVariable(UserEnvironmentVariable);
                if (!string.IsNullOrWhiteSpace(user))
                {
                    options.User = user;
                }

                var password = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable);
                if (!string.IsNullOrWhiteSpace(password))
                {
                    options.Password = password;
                }

                if (bool.TryParse(Environment.GetEnvironmentVariable(SslEnvironmentVariable), out var useSsl))
                {
                    options.Ssl = useSsl;
                }
            },
            cancellationToken);
}
