using RedisVlDotNet.Caches;
using RedisVlDotNet.Tests.Indexes;
using StackExchange.Redis;

namespace RedisVlDotNet.Tests.Caches;

public sealed class EmbeddingsCacheIntegrationTests
{
    [RedisSearchIntegrationFact]
    public async Task StoresLooksUpOverwritesAndExpiresEmbeddings()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(RedisSearchTestEnvironment.ConnectionString!);
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var cache = new EmbeddingsCache(
            database,
            new EmbeddingsCacheOptions("integration-cache", token, TimeSpan.FromMilliseconds(250)));

        var firstEmbedding = new[] { 1f, 2f, 3f };
        var updatedEmbedding = new[] { 4f, 5f, 6f };

        var missing = await cache.LookupAsync("missing");
        var stored = await cache.StoreAsync("prompt", firstEmbedding);
        var hit = await cache.LookupAsync("prompt");
        var overwritten = await cache.StoreAsync("prompt", updatedEmbedding);
        var overwrittenHit = await cache.LookupAsync("prompt");

        Assert.Null(missing);
        Assert.True(stored);
        Assert.Equal(firstEmbedding, hit);
        Assert.True(overwritten);
        Assert.Equal(updatedEmbedding, overwrittenHit);

        await Task.Delay(TimeSpan.FromMilliseconds(400));

        var expired = await cache.LookupAsync("prompt");

        Assert.Null(expired);
    }
}
