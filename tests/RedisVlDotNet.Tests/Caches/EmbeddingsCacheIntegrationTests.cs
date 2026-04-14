using RedisVl.Caches;
using RedisVl.Tests.Indexes;
using StackExchange.Redis;

namespace RedisVl.Tests.Caches;

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
}
