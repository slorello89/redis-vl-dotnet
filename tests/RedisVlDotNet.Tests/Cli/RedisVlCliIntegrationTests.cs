using RedisVlDotNet.Cli;
using RedisVlDotNet.Indexes;
using RedisVlDotNet.Tests.Indexes;

namespace RedisVlDotNet.Tests.Cli;

public sealed class RedisVlCliIntegrationTests
{
    [RedisSearchIntegrationFact]
    public async Task CreatesInspectsListsAndDeletesIndexesThroughCli()
    {
        var token = Guid.NewGuid().ToString("N");
        var indexName = $"cli-movies-idx-{token}";
        var prefix = $"cli:movie:{token}:";
        var application = new RedisVlCliApplication(new RedisVlCliService());
        using var output = new StringWriter();
        using var error = new StringWriter();

        var createExitCode = await application.RunAsync(
            [
                "index",
                "create",
                "--redis", RedisSearchTestEnvironment.ConnectionString!,
                "--name", indexName,
                "--prefix", prefix,
                "--storage", "hash",
                "--field", "text:title",
                "--field", "tag:genre"
            ],
            output,
            error);

        Assert.Equal(0, createExitCode);
        Assert.Contains($"Created index '{indexName}'.", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());

        output.GetStringBuilder().Clear();

        var infoExitCode = await application.RunAsync(
            ["index", "info", "--redis", RedisSearchTestEnvironment.ConnectionString!, "--name", indexName],
            output,
            error);

        Assert.Equal(0, infoExitCode);
        Assert.Contains($"\"name\": \"{indexName}\"", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"fields\": [", output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();

        var listExitCode = await application.RunAsync(
            ["index", "list", "--redis", RedisSearchTestEnvironment.ConnectionString!],
            output,
            error);

        Assert.Equal(0, listExitCode);
        Assert.Contains(indexName, output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();

        var deleteExitCode = await application.RunAsync(
            ["index", "delete", "--redis", RedisSearchTestEnvironment.ConnectionString!, "--name", indexName],
            output,
            error);

        Assert.Equal(0, deleteExitCode);
        Assert.Contains($"Deleted index '{indexName}'.", output.ToString(), StringComparison.Ordinal);

        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();
        var indexNames = (await SearchIndex.ListAsync(database)).Select(static item => item.Name).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(indexName, indexNames);
    }
}
