using RedisVlDotNet.Caches;
using RedisVlDotNet.Tests.Indexes;
using RedisVlDotNet.Schema;
using StackExchange.Redis;

namespace RedisVlDotNet.Tests.Caches;

public sealed class SemanticCacheIntegrationTests
{
    [RedisSearchIntegrationFact]
    public async Task CreatesStoresAndChecksSemanticMatches()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var options = CreateOptions(token, 0.25d);
        var cache = new SemanticCache(database, options);

        try
        {
            await cache.CreateAsync();
            await cache.StoreAsync("prompt-a", "cached-a", [1f, 0f]);
            await RedisSearchTestEnvironment.WaitForAsync(
                async () => await cache.CheckAsync("prompt-a", [1f, 0f]) is not null);

            var hit = await cache.CheckAsync("prompt-b", [1.2f, 0f]);

            Assert.NotNull(hit);
            Assert.Equal("prompt-a", hit!.Prompt);
            Assert.Equal("cached-a", hit.Response);
            Assert.InRange(hit.Distance, 0d, 0.25d);
        }
        finally
        {
            if (await cache.ExistsAsync())
            {
                await cache.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ReturnsMissWhenNearestPromptFallsOutsideThreshold()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var cache = new SemanticCache(database, CreateOptions(token, 0.25d));

        try
        {
            await cache.CreateAsync();
            await cache.StoreAsync("prompt-a", "cached-a", [1f, 0f]);
            await RedisSearchTestEnvironment.WaitForAsync(
                async () => await cache.CheckAsync("prompt-a", [1f, 0f]) is not null);

            var miss = await cache.CheckAsync("prompt-c", [0f, 1f]);

            Assert.Null(miss);
        }
        finally
        {
            if (await cache.ExistsAsync())
            {
                await cache.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task HonorsThresholdBoundaryAcrossCacheInstances()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var permissiveCache = new SemanticCache(database, CreateOptions(token, 0.25d));
        var strictCache = new SemanticCache(database, CreateOptions(token, 0.15d));

        try
        {
            await permissiveCache.CreateAsync();
            await permissiveCache.StoreAsync("prompt-a", "cached-a", [1f, 0f]);
            await RedisSearchTestEnvironment.WaitForAsync(
                async () => await permissiveCache.CheckAsync("prompt-a", [1f, 0f]) is not null);

            var permissiveHit = await permissiveCache.CheckAsync("prompt-b", [1.2f, 0f]);
            var strictMiss = await strictCache.CheckAsync("prompt-b", [1.2f, 0f]);

            Assert.NotNull(permissiveHit);
            Assert.Null(strictMiss);
        }
        finally
        {
            if (await permissiveCache.ExistsAsync())
            {
                await permissiveCache.DropAsync(deleteDocuments: true);
            }
        }
    }

    private static SemanticCacheOptions CreateOptions(string token, double distanceThreshold) =>
        new(
            "integration-semantic-cache",
            new VectorFieldAttributes(
                VectorAlgorithm.Flat,
                VectorDataType.Float32,
                VectorDistanceMetric.L2,
                2),
            distanceThreshold,
            token,
            TimeSpan.FromMinutes(1));
}
