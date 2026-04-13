using StackExchange.Redis;

namespace RedisVlDotNet;

public static class RedisConnectionFactory
{
    private const int DefaultRedisPort = 6379;
    private const int DefaultSentinelPort = 26379;

    public static ConfigurationOptions CreateSentinelOptions(
        string sentinelNodes,
        string serviceName,
        Action<ConfigurationOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sentinelNodes);
        return CreateSentinelOptions(SplitNodes(sentinelNodes), serviceName, configure);
    }

    public static ConfigurationOptions CreateSentinelOptions(
        IEnumerable<string> sentinelNodes,
        string serviceName,
        Action<ConfigurationOptions>? configure = null)
    {
        var normalizedSentinelNodes = NormalizeNodes(sentinelNodes, "sentinel");
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            CommandMap = CommandMap.Sentinel,
            ServiceName = serviceName,
            TieBreaker = string.Empty,
        };

        foreach (var sentinelNode in normalizedSentinelNodes)
        {
            AddNode(options, sentinelNode, "sentinel", DefaultSentinelPort);
        }

        configure?.Invoke(options);
        ValidateSentinelOptions(options);

        return options;
    }

    public static ConfigurationOptions CreateClusterOptions(
        string seedNodes,
        Action<ConfigurationOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedNodes);
        return CreateClusterOptions(SplitNodes(seedNodes), configure);
    }

    public static ConfigurationOptions CreateClusterOptions(
        IEnumerable<string> seedNodes,
        Action<ConfigurationOptions>? configure = null)
    {
        var normalizedSeedNodes = NormalizeNodes(seedNodes, "cluster seed");
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            DefaultDatabase = 0,
        };

        foreach (var seedNode in normalizedSeedNodes)
        {
            AddNode(options, seedNode, "cluster seed", DefaultRedisPort);
        }

        configure?.Invoke(options);
        ValidateClusterOptions(options);

        return options;
    }

    public static Task<IConnectionMultiplexer> ConnectSentinelAsync(
        string sentinelNodes,
        string serviceName,
        Action<ConfigurationOptions>? configure = null,
        CancellationToken cancellationToken = default) =>
        ConnectSentinelAsync(SplitNodes(sentinelNodes), serviceName, configure, cancellationToken);

    public static async Task<IConnectionMultiplexer> ConnectSentinelAsync(
        IEnumerable<string> sentinelNodes,
        string serviceName,
        Action<ConfigurationOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = CreateSentinelOptions(sentinelNodes, serviceName, configure);
        return await ConnectionMultiplexer.SentinelConnectAsync(options)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public static Task<IConnectionMultiplexer> ConnectSentinelPrimaryAsync(
        string sentinelNodes,
        string serviceName,
        Action<ConfigurationOptions>? configure = null,
        CancellationToken cancellationToken = default) =>
        ConnectSentinelPrimaryAsync(SplitNodes(sentinelNodes), serviceName, configure, cancellationToken);

    public static async Task<IConnectionMultiplexer> ConnectSentinelPrimaryAsync(
        IEnumerable<string> sentinelNodes,
        string serviceName,
        Action<ConfigurationOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = CreateSentinelOptions(sentinelNodes, serviceName, configure);
        var sentinel = await ConnectionMultiplexer.SentinelConnectAsync(options)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        return sentinel.GetSentinelMasterConnection(options);
    }

    public static Task<IConnectionMultiplexer> ConnectClusterAsync(
        string seedNodes,
        Action<ConfigurationOptions>? configure = null,
        CancellationToken cancellationToken = default) =>
        ConnectClusterAsync(SplitNodes(seedNodes), configure, cancellationToken);

    public static async Task<IConnectionMultiplexer> ConnectClusterAsync(
        IEnumerable<string> seedNodes,
        Action<ConfigurationOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = CreateClusterOptions(seedNodes, configure);
        return await ConnectionMultiplexer.ConnectAsync(options).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> SplitNodes(string nodes) =>
        nodes.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<string> NormalizeNodes(IEnumerable<string> nodes, string nodeDescription)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var normalizedNodes = nodes
            .Select(static node => node?.Trim())
            .Where(static node => !string.IsNullOrWhiteSpace(node))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();

        if (normalizedNodes.Length == 0)
        {
            throw new ArgumentException($"At least one Redis {nodeDescription} node is required.", nameof(nodes));
        }

        return normalizedNodes;
    }

    private static void AddNode(
        ConfigurationOptions options,
        string node,
        string nodeDescription,
        int defaultPort)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(node);

        var hasExplicitPort = TryParseHostAndPort(node, nodeDescription, defaultPort, out var host, out var port);
        if (hasExplicitPort)
        {
            options.EndPoints.Add(host, port);
            return;
        }

        options.EndPoints.Add(host, defaultPort);
    }

    private static bool TryParseHostAndPort(
        string value,
        string nodeDescription,
        int defaultPort,
        out string host,
        out int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.StartsWith('['))
        {
            var closingBracketIndex = value.IndexOf(']');
            if (closingBracketIndex < 0)
            {
                throw new ArgumentException(
                    $"Redis {nodeDescription} node '{value}' is invalid. IPv6 addresses must use the '[addr]:port' format.",
                    nameof(value));
            }

            host = value[1..closingBracketIndex];
            if (closingBracketIndex == value.Length - 1)
            {
                port = defaultPort;
                return false;
            }

            if (value[closingBracketIndex + 1] != ':' ||
                !int.TryParse(value[(closingBracketIndex + 2)..], out port) ||
                port <= 0)
            {
                throw new ArgumentException(
                    $"Redis {nodeDescription} node '{value}' is invalid. Expected '[addr]:port' or '[addr]'.",
                    nameof(value));
            }

            return true;
        }

        var colonIndex = value.LastIndexOf(':');
        if (colonIndex < 0)
        {
            host = value;
            port = defaultPort;
            return false;
        }

        if (value.IndexOf(':') != colonIndex)
        {
            throw new ArgumentException(
                $"Redis {nodeDescription} node '{value}' is invalid. IPv6 addresses must use the '[addr]:port' format.",
                nameof(value));
        }

        host = value[..colonIndex].Trim();
        var portSegment = value[(colonIndex + 1)..].Trim();
        if (host.Length == 0 || !int.TryParse(portSegment, out port) || port <= 0)
        {
            throw new ArgumentException(
                $"Redis {nodeDescription} node '{value}' is invalid. Expected 'host:port'.",
                nameof(value));
        }

        return true;
    }

    private static void ValidateSentinelOptions(ConfigurationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.EndPoints.Count == 0)
        {
            throw new ArgumentException("At least one Redis sentinel node is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.ServiceName))
        {
            throw new ArgumentException("Redis Sentinel connections require a non-empty service name.", nameof(options));
        }

        if (!ReferenceEquals(options.CommandMap, CommandMap.Sentinel))
        {
            throw new ArgumentException(
                "Redis Sentinel connections must use CommandMap.Sentinel.",
                nameof(options));
        }
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
