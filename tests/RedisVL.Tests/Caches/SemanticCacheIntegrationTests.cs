using RedisVL.Caches;
using RedisVL.Filters;
using RedisVL.Indexes;
using RedisVL.Schema;
using RedisVL.Tests.Indexes;

namespace RedisVL.Tests.Caches;

public sealed class SemanticCacheIntegrationTests
{
    [RedisSearchIntegrationFact]
    public async Task CreatesStoresAndChecksSemanticMatchesWithMetadata()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var options = CreateOptions(token, 0.25d);
        var cache = new SemanticCache(database, options);

        try
        {
            await cache.CreateAsync();
            await cache.StoreAsync(
                "prompt-a",
                "cached-a",
                [1f, 0f],
                metadata: new { tenant = "team-a", source = "faq" },
                filterValues: new Dictionary<string, object?>
                {
                    ["tenant"] = "team-a",
                    ["temperature"] = 0.2d
                });
            await RedisSearchTestEnvironment.WaitForAsync(
                async () => await cache.CheckAsync("prompt-a", [1f, 0f], Filter.Tag("tenant").Eq("team-a")) is not null);

            var hit = await cache.CheckAsync("prompt-b", [1.2f, 0f], Filter.Tag("tenant").Eq("team-a"));

            Assert.NotNull(hit);
            Assert.Equal("prompt-a", hit!.Prompt);
            Assert.Equal("cached-a", hit.Response);
            Assert.Equal("{\"tenant\":\"team-a\",\"source\":\"faq\"}", hit.Metadata);
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
            await cache.StoreAsync("prompt-a", "cached-a", [1f, 0f], filterValues: TeamAFilterValues);
            await RedisSearchTestEnvironment.WaitForAsync(
                async () => await cache.CheckAsync("prompt-a", [1f, 0f], Filter.Tag("tenant").Eq("team-a")) is not null);

            var miss = await cache.CheckAsync("prompt-c", [0f, 1f], Filter.Tag("tenant").Eq("team-a"));

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
            await permissiveCache.StoreAsync("prompt-a", "cached-a", [1f, 0f], filterValues: TeamAFilterValues);
            await RedisSearchTestEnvironment.WaitForAsync(
                async () => await permissiveCache.CheckAsync("prompt-a", [1f, 0f], Filter.Tag("tenant").Eq("team-a")) is not null);

            var permissiveHit = await permissiveCache.CheckAsync("prompt-b", [1.4f, 0f], Filter.Tag("tenant").Eq("team-a"));
            var strictMiss = await strictCache.CheckAsync("prompt-b", [1.4f, 0f], Filter.Tag("tenant").Eq("team-a"));

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

    [RedisSearchIntegrationFact]
    public async Task HonorsFilterDuringLookupForPromptVariants()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var cache = new SemanticCache(database, CreateOptions(token, 0.25d));

        try
        {
            await cache.CreateAsync();
            await cache.StoreAsync("shared prompt", "cached-a", [1f, 0f], filterValues: TeamAFilterValues);
            await cache.StoreAsync(
                "shared prompt",
                "cached-b",
                [1f, 0f],
                filterValues: new Dictionary<string, object?>
                {
                    ["tenant"] = "team-b",
                    ["temperature"] = 0.2d
                });
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(
                SearchIndex.FromExisting(database, $"semantic-cache:integration-semantic-cache:{token}"),
                2);

            var teamAHit = await cache.CheckAsync("shared prompt", [1f, 0f], Filter.Tag("tenant").Eq("team-a"));
            var teamBHit = await cache.CheckAsync("shared prompt", [1f, 0f], Filter.Tag("tenant").Eq("team-b"));
            var missingTenant = await cache.CheckAsync("shared prompt", [1f, 0f], Filter.Tag("tenant").Eq("team-c"));

            Assert.Equal("cached-a", teamAHit!.Response);
            Assert.Equal("cached-b", teamBHit!.Response);
            Assert.Null(missingTenant);
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
    public async Task ExpiresCachedEntriesAfterTimeToLive()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var cache = new SemanticCache(database, CreateOptions(token, 0.25d, TimeSpan.FromSeconds(1)));

        try
        {
            await cache.CreateAsync();
            await cache.StoreAsync("prompt-a", "cached-a", [1f, 0f], filterValues: TeamAFilterValues);
            await RedisSearchTestEnvironment.WaitForAsync(
                async () => await cache.CheckAsync("prompt-a", [1f, 0f], Filter.Tag("tenant").Eq("team-a")) is not null);

            await RedisSearchTestEnvironment.WaitForAsync(
                async () => await cache.CheckAsync("prompt-a", [1f, 0f], Filter.Tag("tenant").Eq("team-a")) is null,
                timeout: TimeSpan.FromSeconds(10));
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
    public async Task SkipIfExistsRejectsIncompatibleFilterSchema()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var originalCache = new SemanticCache(database, CreateOptions(token, 0.25d));
        var incompatibleCache = new SemanticCache(
            database,
            new SemanticCacheOptions(
                "integration-semantic-cache",
                CreateVectorAttributes(),
                0.25d,
                token,
                TimeSpan.FromMinutes(1),
                filterableFields:
                [
                    new TagFieldDefinition("tenant"),
                    new NumericFieldDefinition("priority")
                ]));

        try
        {
            await originalCache.CreateAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                incompatibleCache.CreateAsync(new CreateIndexOptions(skipIfExists: true)));
        }
        finally
        {
            if (await originalCache.ExistsAsync())
            {
                await originalCache.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task StoreManyAndCheckTopKReturnNearestEntriesOrdered()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var cache = new SemanticCache(database, CreateOptions(token, 0.25d));

        try
        {
            await cache.CreateAsync();
            await cache.StoreManyAsync(
            [
                new SemanticCacheStoreRequest("prompt-exact", "resp-exact", [1f, 0f]),
                new SemanticCacheStoreRequest("prompt-near", "resp-near", [0.95f, 0f])
            ]);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(
                SearchIndex.FromExisting(database, $"semantic-cache:integration-semantic-cache:{token}"),
                2);

            var hits = await cache.CheckTopKAsync("query", [1f, 0f], topK: 5);

            Assert.True(hits.Count >= 2);
            Assert.Equal("resp-exact", hits[0].Response);
            Assert.Equal("resp-near", hits[1].Response);
            Assert.True(hits[0].Distance <= hits[1].Distance);
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
    public async Task UpdateRewritesResponseAndMetadataForExistingEntry()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var cache = new SemanticCache(database, CreateOptions(token, 0.25d));

        try
        {
            await cache.CreateAsync();
            var key = await cache.StoreAsync(
                "prompt-a",
                "old response",
                [1f, 0f],
                metadata: new { edited = false },
                filterValues: TeamAFilterValues);
            await RedisSearchTestEnvironment.WaitForAsync(
                async () => await cache.CheckAsync("prompt-a", [1f, 0f], Filter.Tag("tenant").Eq("team-a")) is not null);

            var updated = await cache.UpdateAsync(key, response: "new response", metadata: new { edited = true });
            var missing = await cache.UpdateAsync($"{key}-absent", response: "ignored");

            Assert.True(updated);
            Assert.False(missing);
            await RedisSearchTestEnvironment.WaitForAsync(
                async () =>
                {
                    var hit = await cache.CheckAsync("prompt-a", [1f, 0f], Filter.Tag("tenant").Eq("team-a"));
                    return hit?.Response == "new response";
                });

            var refreshed = await cache.CheckAsync("prompt-a", [1f, 0f], Filter.Tag("tenant").Eq("team-a"));
            Assert.Equal("new response", refreshed!.Response);
            Assert.Equal("{\"edited\":true}", refreshed.Metadata);
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
    public async Task TracksHitAndMissStatisticsWhenEnabled()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var cache = new SemanticCache(database, CreateOptions(token, 0.25d, trackStatistics: true));

        try
        {
            await cache.CreateAsync();
            await cache.StoreAsync("prompt-a", "cached-a", [1f, 0f], filterValues: TeamAFilterValues);
            await RedisSearchTestEnvironment.WaitForAsync(
                async () => await cache.CheckAsync("prompt-a", [1f, 0f], Filter.Tag("tenant").Eq("team-a")) is not null);

            // The warm-up loop above counts toward statistics; reset before the asserted lookups.
            cache.ResetStatistics();

            await cache.CheckAsync("prompt-a", [1f, 0f], Filter.Tag("tenant").Eq("team-a"));
            await cache.CheckAsync("prompt-z", [0f, 1f], Filter.Tag("tenant").Eq("team-a"));

            Assert.Equal(1, cache.HitCount);
            Assert.Equal(1, cache.MissCount);
            Assert.Equal(0.5d, cache.HitRate, 5);
        }
        finally
        {
            if (await cache.ExistsAsync())
            {
                await cache.DropAsync(deleteDocuments: true);
            }
        }
    }

    private static SemanticCacheOptions CreateOptions(
        string token,
        double distanceThreshold,
        TimeSpan? timeToLive = null,
        bool trackStatistics = false) =>
        new(
            "integration-semantic-cache",
            CreateVectorAttributes(),
            distanceThreshold,
            token,
            timeToLive ?? TimeSpan.FromMinutes(1),
            filterableFields:
            [
                new TagFieldDefinition("tenant"),
                new NumericFieldDefinition("temperature")
            ],
            trackStatistics: trackStatistics);

    private static VectorFieldAttributes CreateVectorAttributes() =>
        new(
            VectorAlgorithm.Flat,
            VectorDataType.Float32,
            VectorDistanceMetric.L2,
            2);

    private static readonly IReadOnlyDictionary<string, object?> TeamAFilterValues =
        new Dictionary<string, object?>
        {
            ["tenant"] = "team-a",
            ["temperature"] = 0.2d
        };
}
