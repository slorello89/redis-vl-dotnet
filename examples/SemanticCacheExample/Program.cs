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
        ],
        trackStatistics: true));

try
{
    await cache.CreateAsync();

    var teamAFilters = new Dictionary<string, object?>
    {
        ["tenant"] = "team-a",
        ["model"] = "gpt-4.1-mini",
        ["temperature"] = 0.2d
    };

    var passwordKey = await cache.StoreAsync(
        "How do I reset my password?",
        "Open Settings > Security > Reset password and follow the email link.",
        [1f, 0f, 0f],
        metadata: new
        {
            source = "faq",
            tags = new[] { "account", "self-serve" }
        },
        filterValues: teamAFilters);

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

    // Batch-store several related team-a FAQ entries in one call.
    await cache.StoreManyAsync(
    [
        new SemanticCacheStoreRequest(
            "Where do I change my password?",
            "Settings > Security has the password controls.",
            [0.97f, 0.05f, 0f],
            FilterValues: teamAFilters),
        new SemanticCacheStoreRequest(
            "Reset login credentials",
            "Use the 'Forgot password' link on the sign-in page.",
            [0.93f, 0.08f, 0f],
            FilterValues: teamAFilters)
    ]);

    var hit = await cache.CheckAsync(
        "Need help resetting my password",
        [0.98f, 0.01f, 0f],
        Filter.And(
            Filter.Tag("tenant").Eq("team-a"),
            Filter.Numeric("temperature").Eq(0.2d)));

    Console.WriteLine(hit is null
        ? "No cache hit."
        : $"Single hit: {hit.Response}\nMetadata: {hit.Metadata}\nDistance: {hit.Distance:F4}");

    // Top-K: return the nearest cached entries within the distance threshold, ordered nearest-first.
    var topHits = await cache.CheckTopKAsync(
        "How can I reset my account password?",
        [0.97f, 0.03f, 0f],
        topK: 3,
        Filter.Tag("tenant").Eq("team-a"));

    Console.WriteLine($"\nTop-{topHits.Count} team-a matches:");
    foreach (var topHit in topHits)
    {
        Console.WriteLine($"- distance={topHit.Distance:F4} | {topHit.Response}");
    }

    // Update the stored response/metadata in place (the embedding is unchanged).
    var updated = await cache.UpdateAsync(
        passwordKey,
        response: "Open Settings > Security > Reset password, then check your email for the link.",
        metadata: new { source = "faq", revised = true });
    Console.WriteLine($"\nUpdated stored entry: {updated}");

    // A deliberate miss to exercise the statistics counters.
    _ = await cache.CheckAsync(
        "What is the weather today?",
        [0f, 0f, 1f],
        Filter.Tag("tenant").Eq("team-a"));

    Console.WriteLine(
        $"\nCache statistics — hits={cache.HitCount}, misses={cache.MissCount}, hit rate={cache.HitRate:P0}");
}
finally
{
    if (await cache.ExistsAsync())
    {
        await cache.DropAsync(deleteDocuments: true);
    }
}
