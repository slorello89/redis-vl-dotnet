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
var firstEmbedding = new[] { 0.10f, 0.20f, 0.30f };
var updatedEmbedding = new[] { 0.90f, 0.10f, 0.05f };

var initialMiss = await cache.LookupAsync(input);
Console.WriteLine(initialMiss is null ? "Initial lookup: miss" : "Initial lookup: unexpected hit");

await cache.StoreAsync(input, firstEmbedding);
var stored = await cache.LookupAsync(input);
Console.WriteLine($"Stored embedding: [{string.Join(", ", stored ?? [])}]");

await cache.StoreAsync(input, updatedEmbedding);
var overwritten = await cache.LookupAsync(input);
Console.WriteLine($"Overwritten embedding: [{string.Join(", ", overwritten ?? [])}]");

Console.WriteLine("Entries are isolated by namespace and expire automatically after the configured TTL.");
