using StackExchange.Redis;

namespace RedisVlDotNet;

public static class RedisConnectionFactory
{
    private const int DefaultRedisPort = 6379;

    public static ConfigurationOptions CreateClusterOptions(
        string seedNodes,
        Action<ConfigurationOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedNodes);
        return CreateClusterOptions(SplitSeedNodes(seedNodes), configure);
    }

    public static ConfigurationOptions CreateClusterOptions(
        IEnumerable<string> seedNodes,
        Action<ConfigurationOptions>? configure = null)
    {
        var normalizedSeedNodes = NormalizeSeedNodes(seedNodes);
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            DefaultDatabase = 0,
        };

        foreach (var seedNode in normalizedSeedNodes)
        {
            AddSeedNode(options, seedNode);
        }

        configure?.Invoke(options);
        ValidateClusterOptions(options);

        return options;
    }

    public static Task<IConnectionMultiplexer> ConnectClusterAsync(
        string seedNodes,
        Action<ConfigurationOptions>? configure = null,
        CancellationToken cancellationToken = default) =>
        ConnectClusterAsync(SplitSeedNodes(seedNodes), configure, cancellationToken);

    public static async Task<IConnectionMultiplexer> ConnectClusterAsync(
        IEnumerable<string> seedNodes,
        Action<ConfigurationOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = CreateClusterOptions(seedNodes, configure);
        return await ConnectionMultiplexer.ConnectAsync(options).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> SplitSeedNodes(string seedNodes) =>
        seedNodes
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<string> NormalizeSeedNodes(IEnumerable<string> seedNodes)
    {
        ArgumentNullException.ThrowIfNull(seedNodes);

        var normalizedSeedNodes = seedNodes
            .Select(static node => node?.Trim())
            .Where(static node => !string.IsNullOrWhiteSpace(node))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();

        if (normalizedSeedNodes.Length == 0)
        {
            throw new ArgumentException("At least one Redis cluster seed node is required.", nameof(seedNodes));
        }

        return normalizedSeedNodes;
    }

    private static void AddSeedNode(ConfigurationOptions options, string seedNode)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(seedNode);

        var hasExplicitPort = TryParseHostAndPort(seedNode, out var host, out var port);
        if (hasExplicitPort)
        {
            options.EndPoints.Add(host, port);
            return;
        }

        options.EndPoints.Add(host, DefaultRedisPort);
    }

    private static bool TryParseHostAndPort(string value, out string host, out int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.StartsWith('['))
        {
            var closingBracketIndex = value.IndexOf(']');
            if (closingBracketIndex < 0)
            {
                throw new ArgumentException(
                    $"Cluster seed node '{value}' is invalid. IPv6 addresses must use the '[addr]:port' format.",
                    nameof(value));
            }

            host = value[1..closingBracketIndex];
            if (closingBracketIndex == value.Length - 1)
            {
                port = DefaultRedisPort;
                return false;
            }

            if (value[closingBracketIndex + 1] != ':' ||
                !int.TryParse(value[(closingBracketIndex + 2)..], out port) ||
                port <= 0)
            {
                throw new ArgumentException(
                    $"Cluster seed node '{value}' is invalid. Expected '[addr]:port' or '[addr]'.",
                    nameof(value));
            }

            return true;
        }

        var colonIndex = value.LastIndexOf(':');
        if (colonIndex < 0)
        {
            host = value;
            port = DefaultRedisPort;
            return false;
        }

        if (value.IndexOf(':') != colonIndex)
        {
            throw new ArgumentException(
                $"Cluster seed node '{value}' is invalid. IPv6 addresses must use the '[addr]:port' format.",
                nameof(value));
        }

        host = value[..colonIndex].Trim();
        var portSegment = value[(colonIndex + 1)..].Trim();
        if (host.Length == 0 || !int.TryParse(portSegment, out port) || port <= 0)
        {
            throw new ArgumentException(
                $"Cluster seed node '{value}' is invalid. Expected 'host:port'.",
                nameof(value));
        }

        return true;
    }

    private static void ValidateClusterOptions(ConfigurationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.EndPoints.Count == 0)
        {
            throw new ArgumentException("At least one Redis cluster seed node is required.", nameof(options));
        }

        if (options.DefaultDatabase is not null && options.DefaultDatabase != 0)
        {
            throw new ArgumentException("Redis cluster connections must use database 0.", nameof(options));
        }
    }
}
