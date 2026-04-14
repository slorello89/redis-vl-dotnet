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

var initialMiss = await cache.LookupAsync(input, modelName);
Console.WriteLine(initialMiss is null ? "Initial lookup: miss" : "Initial lookup: unexpected hit");

await cache.StoreAsync(input, modelName, firstEmbedding, metadata: new { source = "backlog-summary" });
var stored = await cache.LookupAsync(input, modelName);
Console.WriteLine($"Stored entry: model={stored?.ModelName}, metadata={stored?.Metadata}, embedding=[{string.Join(", ", stored?.Embedding ?? [])}]");

await cache.StoreAsync(input, modelName, updatedEmbedding, metadata: new { source = "backlog-summary", revision = 2 });
var overwritten = await cache.LookupAsync(input, modelName);
Console.WriteLine($"Overwritten entry: model={overwritten?.ModelName}, metadata={overwritten?.Metadata}, embedding=[{string.Join(", ", overwritten?.Embedding ?? [])}]");

Console.WriteLine("Entries are isolated by namespace and expire automatically after the configured TTL.");
