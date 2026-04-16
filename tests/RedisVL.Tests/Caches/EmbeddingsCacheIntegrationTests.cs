using RedisVL.Caches;
using RedisVL.Tests.Indexes;
using StackExchange.Redis;

namespace RedisVL.Tests.Caches;

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
        Assert.Equal("prompt", hit!.Input);
        Assert.Equal(firstEmbedding, hit.Embedding);
        Assert.True(overwritten);
        Assert.Equal(updatedEmbedding, overwrittenHit!.Embedding);

        await Task.Delay(TimeSpan.FromMilliseconds(400));

        var expired = await cache.LookupAsync("prompt");

        Assert.Null(expired);
    }

    [RedisSearchIntegrationFact]
    public async Task StoresDistinctEntriesForSameInputAcrossModels()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(RedisSearchTestEnvironment.ConnectionString!);
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("integration-cache", token));
        var smallEmbedding = new[] { 1f, 2f, 3f };
        var largeEmbedding = new[] { 4f, 5f, 6f };

        var smallStored = await cache.StoreAsync("prompt", "text-embedding-3-small", smallEmbedding);
        var largeStored = await cache.StoreAsync("prompt", "text-embedding-3-large", largeEmbedding);
        var smallHit = await cache.LookupAsync("prompt", "text-embedding-3-small");
        var largeHit = await cache.LookupAsync("prompt", "text-embedding-3-large");
        var legacyMiss = await cache.LookupAsync("prompt");

        Assert.True(smallStored);
        Assert.True(largeStored);
        Assert.Equal("text-embedding-3-small", smallHit!.ModelName);
        Assert.Equal(smallEmbedding, smallHit.Embedding);
        Assert.Equal("text-embedding-3-large", largeHit!.ModelName);
        Assert.Equal(largeEmbedding, largeHit.Embedding);
        Assert.Null(legacyMiss);
    }

    [RedisSearchIntegrationFact]
    public async Task StoresAndReturnsMetadataPayload()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(RedisSearchTestEnvironment.ConnectionString!);
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("integration-cache", token));

        var stored = await cache.StoreAsync(
            "prompt",
            "text-embedding-3-small",
            [1f, 2f, 3f],
            metadata: new { source = "faq", tenant = "team-a" });
        var hit = await cache.LookupAsync("prompt", "text-embedding-3-small");

        Assert.True(stored);
        Assert.NotNull(hit);
        Assert.Equal("{\"source\":\"faq\",\"tenant\":\"team-a\"}", hit!.Metadata);
        Assert.Equal([1f, 2f, 3f], hit.Embedding);
    }

    [RedisSearchIntegrationFact]
    public async Task ReadsAndChecksExistenceByKeyAndSemanticLookup()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(RedisSearchTestEnvironment.ConnectionString!);
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("integration-cache", token));

        var stored = await cache.SetAsync("prompt", "text-embedding-3-small", [1f, 2f, 3f]);
        var directHit = await cache.GetByKeyAsync(stored.Key!);
        var directMiss = await cache.GetByKeyAsync($"{stored.Key}:missing");
        var semanticHit = await cache.ExistsAsync("prompt", "text-embedding-3-small");
        var semanticMiss = await cache.ExistsAsync("prompt", "text-embedding-3-large");
        var directExists = await cache.ExistsByKeyAsync(stored.Key!);
        var directMissing = await cache.ExistsByKeyAsync($"{stored.Key}:missing");

        Assert.NotNull(directHit);
        Assert.Equal(stored.Key, directHit!.Key);
        Assert.Equal("prompt", directHit.Input);
        Assert.Equal("text-embedding-3-small", directHit.ModelName);
        Assert.Null(directMiss);
        Assert.True(semanticHit);
        Assert.False(semanticMiss);
        Assert.True(directExists);
        Assert.False(directMissing);
    }

    [RedisSearchIntegrationFact]
    public async Task DeletesEntriesBySemanticLookupAndDirectKey()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(RedisSearchTestEnvironment.ConnectionString!);
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("integration-cache", token));

        var semanticEntry = await cache.SetAsync("prompt", "text-embedding-3-small", [1f, 2f, 3f]);
        var directEntry = await cache.SetAsync("other prompt", "text-embedding-3-small", [4f, 5f, 6f]);

        var semanticDeleted = await cache.DeleteAsync("prompt", "text-embedding-3-small");
        var semanticRepeatDelete = await cache.DeleteAsync("prompt", "text-embedding-3-small");
        var directDeleted = await cache.DeleteByKeyAsync(directEntry.Key!);
        var directRepeatDelete = await cache.DeleteByKeyAsync(directEntry.Key!);

        Assert.True(semanticDeleted);
        Assert.False(semanticRepeatDelete);
        Assert.True(directDeleted);
        Assert.False(directRepeatDelete);
        Assert.Null(await cache.GetAsync("prompt", "text-embedding-3-small"));
        Assert.Null(await cache.GetByKeyAsync(directEntry.Key!));
        Assert.False(await cache.ExistsByKeyAsync(semanticEntry.Key!));
        Assert.False(await cache.ExistsByKeyAsync(directEntry.Key!));
    }

    [RedisSearchIntegrationFact]
    public async Task SupportsBatchOperationsWithMixedHitsAndMisses()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(RedisSearchTestEnvironment.ConnectionString!);
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var cache = new EmbeddingsCache(
            database,
            new EmbeddingsCacheOptions("integration-cache", token, TimeSpan.FromMinutes(5)));

        var stored = await cache.SetManyAsync(
        [
            new EmbeddingsCacheWriteRequest("alpha", [1f, 2f], "model-a"),
            new EmbeddingsCacheWriteRequest("beta", [3f, 4f], "model-b", metadata: new { source = "faq" })
        ]);

        var semanticHits = await cache.GetManyAsync(
        [
            new EmbeddingsCacheLookup("beta", "model-b"),
            new EmbeddingsCacheLookup("missing", "model-x"),
            new EmbeddingsCacheLookup("alpha", "model-a")
        ]);
        var directHits = await cache.GetManyByKeyAsync([stored[1].Key!, $"{stored[1].Key}:missing", stored[0].Key!]);
        var semanticExists = await cache.ExistsManyAsync(
        [
            new EmbeddingsCacheLookup("alpha", "model-a"),
            new EmbeddingsCacheLookup("alpha", "model-x")
        ]);
        var directExists = await cache.ExistsManyByKeyAsync([stored[0].Key!, $"{stored[0].Key}:missing"]);
        var semanticDeleted = await cache.DeleteManyAsync(
        [
            new EmbeddingsCacheLookup("alpha", "model-a"),
            new EmbeddingsCacheLookup("missing", "model-x")
        ]);
        var directDeleted = await cache.DeleteManyByKeyAsync([stored[1].Key!, $"{stored[1].Key}:missing"]);

        Assert.Equal(2, stored.Count);
        Assert.Equal("beta", semanticHits[0]!.Input);
        Assert.Null(semanticHits[1]);
        Assert.Equal("alpha", semanticHits[2]!.Input);
        Assert.Equal("beta", directHits[0]!.Input);
        Assert.Null(directHits[1]);
        Assert.Equal("alpha", directHits[2]!.Input);
        Assert.Equal([true, false], semanticExists);
        Assert.Equal([true, false], directExists);
        Assert.Equal(1, semanticDeleted);
        Assert.Equal(1, directDeleted);
        Assert.Null(await cache.GetAsync("alpha", "model-a"));
        Assert.Null(await cache.GetByKeyAsync(stored[1].Key!));
    }
}
