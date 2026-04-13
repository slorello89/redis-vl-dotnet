using System.Net;
using StackExchange.Redis;

namespace RedisVlDotNet.Tests;

public sealed class RedisConnectionFactoryTests
{
    [Fact]
    public void CreateClusterOptions_FromDelimitedString_NormalizesSeedNodesAndDefaults()
    {
        var options = RedisConnectionFactory.CreateClusterOptions(" redis-1:7000,redis-2 ; redis-2 \n redis-3:7002 ");
        var endpoints = options.EndPoints.Select(static endpoint => endpoint switch
        {
            DnsEndPoint dns => $"{dns.Host}:{dns.Port}",
            IPEndPoint ip => $"{ip.Address}:{ip.Port}",
            _ => endpoint.ToString()!
        }).ToArray();

        Assert.Equal(3, options.EndPoints.Count);
        Assert.Equal(["redis-1:7000", "redis-2:6379", "redis-3:7002"], endpoints);
        Assert.False(options.AbortOnConnectFail);
        Assert.Equal(0, options.DefaultDatabase);
    }

    [Fact]
    public void CreateClusterOptions_ConfigureCallbackCanSetAuthTlsAndClientName()
    {
        var options = RedisConnectionFactory.CreateClusterOptions(
            ["redis-1:7000"],
            configuration =>
            {
                configuration.User = "app-user";
                configuration.Password = "secret";
                configuration.Ssl = true;
                configuration.ClientName = "redis-vl-tests";
            });

        Assert.Equal("app-user", options.User);
        Assert.Equal("secret", options.Password);
        Assert.True(options.Ssl);
        Assert.Equal("redis-vl-tests", options.ClientName);
    }

    [Fact]
    public void CreateClusterOptions_RejectsMissingSeedNodes()
    {
        var exception = Assert.Throws<ArgumentException>(() => RedisConnectionFactory.CreateClusterOptions(" , \n "));

        Assert.Contains("seed node", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateClusterOptions_RejectsInvalidIpv6WithoutBrackets()
    {
        var exception = Assert.Throws<ArgumentException>(() => RedisConnectionFactory.CreateClusterOptions(["fe80::1:7000"]));

        Assert.Contains("IPv6", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateClusterOptions_RejectsNonZeroDatabase()
    {
        var exception = Assert.Throws<ArgumentException>(() => RedisConnectionFactory.CreateClusterOptions(
            ["redis-1:7000"],
            configuration => configuration.DefaultDatabase = 2));

        Assert.Contains("database 0", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConnectClusterAsync_WithCancelledToken_DoesNotAttemptConnection()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => RedisConnectionFactory.ConnectClusterAsync(
            ["redis-1:7000"],
            cancellationToken: cancellationTokenSource.Token));
    }
}
