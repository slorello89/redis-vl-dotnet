using RedisVL.Caches;
using RedisVL.Filters;
using RedisVL.Schema;
using StackExchange.Redis;

var redisUrl = Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL") ?? "localhost:6379";
await using var connection = await ConnectionMultiplexer.ConnectAsync(redisUrl);
var database = connection.GetDatabase();

var cache = new SemanticCache(
    database,
    new SemanticCacheOptions(
        "semantic-cache-example",
        new VectorFieldAttributes(
            VectorAlgorithm.Flat,
            VectorDataType.Float32,
            VectorDistanceMetric.Cosine,
            3),
        distanceThreshold: 0.2d,
        keyNamespace: "examples",
        timeToLive: TimeSpan.FromMinutes(10),
        filterableFields:
        [
            new TagFieldDefinition("tenant"),
            new TagFieldDefinition("model"),
            new NumericFieldDefinition("temperature")
        ]));

try
{
    await cache.CreateAsync();

    await cache.StoreAsync(
        "How do I reset my password?",
        "Open Settings > Security > Reset password and follow the email link.",
        [1f, 0f, 0f],
        metadata: new
        {
            source = "faq",
            tags = new[] { "account", "self-serve" }
        },
        filterValues: new Dictionary<string, object?>
        {
            ["tenant"] = "team-a",
            ["model"] = "gpt-4.1-mini",
            ["temperature"] = 0.2d
        });

    await cache.StoreAsync(
        "How do I reset my password?",
        "Admins can reset passwords from the control panel for enterprise tenants.",
        [1f, 0f, 0f],
        metadata: new
        {
            source = "runbook",
            escalation = true
        },
        filterValues: new Dictionary<string, object?>
        {
            ["tenant"] = "team-b",
            ["model"] = "gpt-4.1-mini",
            ["temperature"] = 0.2d
        });

    var hit = await cache.CheckAsync(
        "Need help resetting my password",
        [0.98f, 0.01f, 0f],
        Filter.And(
            Filter.Tag("tenant").Eq("team-a"),
            Filter.Numeric("temperature").Eq(0.2d)));

    Console.WriteLine(hit is null
        ? "No cache hit."
        : $"Hit: {hit.Response}\nMetadata: {hit.Metadata}\nDistance: {hit.Distance:F4}");
}
finally
{
    if (await cache.ExistsAsync())
    {
        await cache.DropAsync(deleteDocuments: true);
    }
}
