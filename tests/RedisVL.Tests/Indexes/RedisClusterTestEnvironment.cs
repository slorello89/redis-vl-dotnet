using StackExchange.Redis;

namespace RedisVL.Tests.Indexes;

internal static class RedisClusterTestEnvironment
{
    public const string ClusterNodesEnvironmentVariable = "REDIS_VL_REDIS_CLUSTER_NODES";
    public const string UserEnvironmentVariable = "REDIS_VL_REDIS_USER";
    public const string PasswordEnvironmentVariable = "REDIS_VL_REDIS_PASSWORD";
    public const string SslEnvironmentVariable = "REDIS_VL_REDIS_SSL";

    public static string? ClusterNodes =>
        Environment.GetEnvironmentVariable(ClusterNodesEnvironmentVariable);

    public static string? SkipReason =>
        string.IsNullOrWhiteSpace(ClusterNodes)
            ? $"Set {ClusterNodesEnvironmentVariable} to run Redis cluster integration tests."
            : null;

    public static Task<IConnectionMultiplexer> ConnectAsync(CancellationToken cancellationToken = default) =>
        RedisConnectionFactory.ConnectClusterAsync(
            ClusterNodes!,
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
