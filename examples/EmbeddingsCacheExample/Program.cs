using RedisVl.Caches;
using StackExchange.Redis;

var redisUrl = Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL") ?? "localhost:6379";
await using var connection = await ConnectionMultiplexer.ConnectAsync(redisUrl);
var database = connection.GetDatabase();

var cache = new EmbeddingsCache(
    database,
    new EmbeddingsCacheOptions(
        "embeddings-cache-example",
        keyNamespace: Guid.NewGuid().ToString("N"),
        timeToLive: TimeSpan.FromMinutes(10)));

var input = "Summarize the quarterly support backlog";
var modelName = "text-embedding-3-small";
var firstEmbedding = new[] { 0.10f, 0.20f, 0.30f };
var updatedEmbedding = new[] { 0.90f, 0.10f, 0.05f };
var batchEntries = new[]
{
    new EmbeddingsCacheWriteRequest(
        "Summarize the open incidents",
        [0.40f, 0.50f, 0.60f],
        "text-embedding-3-small",
        metadata: new { source = "incidents" }),
    new EmbeddingsCacheWriteRequest(
        "Summarize the billing escalations",
        [0.70f, 0.80f, 0.90f],
        "text-embedding-3-large",
        metadata: new { source = "billing" },
        timeToLive: TimeSpan.FromMinutes(2))
};

var initialMiss = await cache.GetAsync(input, modelName);
Console.WriteLine(initialMiss is null ? "Initial lookup: miss" : "Initial lookup: unexpected hit");

var stored = await cache.SetAsync(
    input,
    modelName,
    firstEmbedding,
    metadata: new { source = "backlog-summary" },
    timeToLive: TimeSpan.FromMinutes(5));
Console.WriteLine($"Stored entry: key={stored.Key}, model={stored.ModelName}, metadata={stored.Metadata}, embedding=[{string.Join(", ", stored.Embedding)}]");

var byKey = await cache.GetByKeyAsync(stored.Key!);
Console.WriteLine($"Read by key: input={byKey?.Input}, model={byKey?.ModelName}");

var existsBeforeDelete = await cache.ExistsAsync(input, modelName);
Console.WriteLine($"Exists before overwrite/delete: {existsBeforeDelete}");

var overwritten = await cache.SetAsync(
    input,
    modelName,
    updatedEmbedding,
    metadata: new { source = "backlog-summary", revision = 2 });
var reread = await cache.GetAsync(input, modelName);
Console.WriteLine($"Overwritten entry: key={overwritten.Key}, model={reread?.ModelName}, metadata={reread?.Metadata}, embedding=[{string.Join(", ", reread?.Embedding ?? [])}]");

var batchStored = await cache.SetManyAsync(batchEntries);
Console.WriteLine($"Batch write stored {batchStored.Count} entries.");

var batchRead = await cache.GetManyAsync(
    [
        new EmbeddingsCacheLookup(batchEntries[0].Input, batchEntries[0].ModelName),
        new EmbeddingsCacheLookup("missing input", "text-embedding-3-small"),
        new EmbeddingsCacheLookup(batchEntries[1].Input, batchEntries[1].ModelName)
    ]);
Console.WriteLine($"Batch read results: [{string.Join(", ", batchRead.Select(entry => entry is null ? "miss" : entry.ModelName ?? "legacy"))}]");

var deletedByKey = await cache.DeleteByKeyAsync(stored.Key!);
var existsAfterDelete = await cache.ExistsAsync(input, modelName);
Console.WriteLine($"Deleted by key: {deletedByKey}; exists after delete: {existsAfterDelete}");

Console.WriteLine("Entries are isolated by namespace, model-aware keys avoid collisions, metadata round-trips as JSON, and per-call TTL overrides the cache default when needed.");
