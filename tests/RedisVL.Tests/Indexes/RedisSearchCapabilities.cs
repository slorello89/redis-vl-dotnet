using StackExchange.Redis;

namespace RedisVL.Tests.Indexes;

/// <summary>
/// Probes the connected server once for the capabilities the integration suite needs to gate on.
/// The native <c>FT.HYBRID</c> command ships with the RediSearch query engine bundled in Redis 8.4+;
/// older servers reject it outright. Without a gate those tests hard-fail on a pre-8.4 server, which
/// is indistinguishable from a real regression. The probe reads the RediSearch module version from
/// <c>MODULE LIST</c> and caches it for the lifetime of the test process.
/// </summary>
internal static class RedisSearchCapabilities
{
    /// <summary>The lowest Redis/RediSearch version that implements the native <c>FT.HYBRID</c> command.</summary>
    public static readonly Version NativeHybridMinimumVersion = new(8, 4, 0);

    private static readonly Lazy<Version?> SearchModuleVersion = new(ProbeSearchModuleVersion);

    /// <summary>
    /// Returns a skip reason when the connected server predates native <c>FT.HYBRID</c> support, or
    /// <see langword="null" /> when the command is available (or the version could not be determined,
    /// in which case the test is allowed to run and fail loudly rather than silently skip).
    /// </summary>
    public static string? NativeHybridSkipReason =>
        SearchModuleVersion.Value is { } version && version < NativeHybridMinimumVersion
            ? $"Requires Redis {NativeHybridMinimumVersion} or newer for the native FT.HYBRID command; connected server reports {version}."
            : null;

    private static Version? ProbeSearchModuleVersion()
    {
        if (string.IsNullOrWhiteSpace(RedisSearchTestEnvironment.ConnectionString))
        {
            return null;
        }

        try
        {
            using var connection = ConnectionMultiplexer.Connect(RedisSearchTestEnvironment.CreateOptions());
            var server = connection.GetServers().FirstOrDefault(static candidate => candidate.IsConnected);
            if (server is null)
            {
                return null;
            }

            var modules = (RedisResult[]?)server.Execute("MODULE", "LIST");
            if (modules is null)
            {
                return null;
            }

            foreach (var module in modules)
            {
                // Each MODULE LIST entry is an alternating key/value collection, e.g.
                // [ "name", "search", "ver", 80800, ... ], over both RESP2 and RESP3.
                var fields = (RedisResult[])module!;
                string? name = null;
                int? encodedVersion = null;
                for (var index = 0; index + 1 < fields.Length; index += 2)
                {
                    switch (fields[index].ToString())
                    {
                        case "name":
                            name = fields[index + 1].ToString();
                            break;
                        case "ver":
                            encodedVersion = (int)fields[index + 1];
                            break;
                    }
                }

                if (string.Equals(name, "search", StringComparison.OrdinalIgnoreCase) && encodedVersion is { } encoded)
                {
                    // RediSearch encodes its version as MMmmpp (e.g. 80400 => 8.4.0).
                    return new Version(encoded / 10000, encoded / 100 % 100, encoded % 100);
                }
            }

            return null;
        }
        catch (RedisException)
        {
            return null;
        }
    }
}
