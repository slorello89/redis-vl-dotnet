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

var initialMiss = await cache.GetAsync(input, modelName);
Console.WriteLine(initialMiss is null ? "Initial lookup: miss" : "Initial lookup: unexpected hit");

var stored = await cache.SetAsync(
    input,
    modelName,
    firstEmbedding,
    metadata: new { source = "backlog-summary" },
    timeToLive: TimeSpan.FromMinutes(5));
Console.WriteLine($"Stored entry: key={stored.Key}, model={stored.ModelName}, metadata={stored.Metadata}, embedding=[{string.Join(", ", stored.Embedding)}]");

var overwritten = await cache.SetAsync(
    input,
    modelName,
    updatedEmbedding,
    metadata: new { source = "backlog-summary", revision = 2 });
var reread = await cache.GetAsync(input, modelName);
Console.WriteLine($"Overwritten entry: key={overwritten.Key}, model={reread?.ModelName}, metadata={reread?.Metadata}, embedding=[{string.Join(", ", reread?.Embedding ?? [])}]");

Console.WriteLine("Entries are isolated by namespace, `Set` returns the Redis key, and per-call TTL overrides the cache default when needed.");
