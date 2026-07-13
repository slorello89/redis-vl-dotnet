using StackExchange.Redis;

namespace RedisVL;

/// <summary>Factory helpers for building StackExchange.Redis connections configured for Redis clusters.</summary>
public static class RedisConnectionFactory
{
    private const int DefaultRedisPort = 6379;

    /// <summary>Builds cluster <see cref="ConfigurationOptions"/> from a delimited list of seed nodes.</summary>
    /// <param name="seedNodes">A comma-, semicolon-, or newline-separated list of <c>host</c> or <c>host:port</c> seed nodes.</param>
    /// <param name="configure">An optional callback to further customize the options before validation.</param>
    /// <returns>The configured <see cref="ConfigurationOptions"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="seedNodes"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static ConfigurationOptions CreateClusterOptions(
        string seedNodes,
        Action<ConfigurationOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedNodes);
        return CreateClusterOptions(SplitNodes(seedNodes), configure);
    }

    /// <summary>Builds cluster <see cref="ConfigurationOptions"/> from a collection of seed nodes.</summary>
    /// <param name="seedNodes">The <c>host</c> or <c>host:port</c> seed nodes; entries are trimmed and de-duplicated.</param>
    /// <param name="configure">An optional callback to further customize the options before validation.</param>
    /// <returns>The configured <see cref="ConfigurationOptions"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when no valid seed nodes are supplied or the resulting options fail cluster validation.</exception>
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

    /// <summary>Connects to a Redis cluster using a delimited list of seed nodes.</summary>
    /// <param name="seedNodes">A comma-, semicolon-, or newline-separated list of <c>host</c> or <c>host:port</c> seed nodes.</param>
    /// <param name="configure">An optional callback to further customize the options before connecting.</param>
    /// <param name="cancellationToken">A token used to cancel the connection attempt.</param>
    /// <returns>A task that resolves to the connected <see cref="IConnectionMultiplexer"/>.</returns>
    public static Task<IConnectionMultiplexer> ConnectClusterAsync(
        string seedNodes,
        Action<ConfigurationOptions>? configure = null,
        CancellationToken cancellationToken = default) =>
        ConnectClusterAsync(SplitNodes(seedNodes), configure, cancellationToken);

    /// <summary>Connects to a Redis cluster using a collection of seed nodes.</summary>
    /// <param name="seedNodes">The <c>host</c> or <c>host:port</c> seed nodes; entries are trimmed and de-duplicated.</param>
    /// <param name="configure">An optional callback to further customize the options before connecting.</param>
    /// <param name="cancellationToken">A token used to cancel the connection attempt.</param>
    /// <returns>A task that resolves to the connected <see cref="IConnectionMultiplexer"/>.</returns>
    public static async Task<IConnectionMultiplexer> ConnectClusterAsync(
        IEnumerable<string> seedNodes,
        Action<ConfigurationOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = CreateClusterOptions(seedNodes, configure);
        var connectTask = ConnectionMultiplexer.ConnectAsync(options);
        try
        {
            return await connectTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // WaitAsync stops awaiting on cancellation but cannot cancel the underlying connect,
            // which may still succeed and produce a live multiplexer nobody holds. Dispose it when
            // (if) it completes so the connection does not leak.
            _ = DisposeWhenCompletedAsync(connectTask);
            throw;
        }
    }

    private static async Task DisposeWhenCompletedAsync(Task<ConnectionMultiplexer> connectTask)
    {
        try
        {
            var multiplexer = await connectTask.ConfigureAwait(false);
            await multiplexer.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // The abandoned connect ultimately failed; there is nothing to dispose.
        }
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
