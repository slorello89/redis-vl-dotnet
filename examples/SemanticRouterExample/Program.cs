using RedisVL.Indexes;
using RedisVL.Schema;
using RedisVL.Vectorizers;
using RedisVL.Workflows;
using StackExchange.Redis;

var redisUrl = Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL") ?? "localhost:6379";
await using var connection = await ConnectionMultiplexer.ConnectAsync(redisUrl);
var database = connection.GetDatabase();

var vectorizer = new KeywordVectorizer();

var router = new SemanticRouter(
    database,
    new SemanticRouterOptions(
        "semantic-router-example",
        new VectorFieldAttributes(
            VectorAlgorithm.Flat,
            VectorDataType.Float32,
            VectorDistanceMetric.Cosine,
            KeywordVectorizer.Dimensions),
        distanceThreshold: 0.8d,
        keyNamespace: "examples",
        routingConfig: new RoutingConfig(maxResults: 2, aggregationMethod: DistanceAggregationMethod.Average)));

try
{
    await router.CreateAsync(new CreateIndexOptions(overwrite: true, dropExistingDocuments: true));

    // Each route carries multiple reference phrases, optional metadata, and an optional per-route threshold.
    var routes = new[]
    {
        new Route(
            "billing",
            ["refund status", "billing question", "payment failed"],
            new Dictionary<string, object?> { ["team"] = "finance" }),
        new Route(
            "shipping",
            ["delivery update", "where is my package", "tracking number"],
            new Dictionary<string, object?> { ["team"] = "logistics" }),
        new Route(
            "support",
            ["reset password", "login problem", "account locked"],
            new Dictionary<string, object?> { ["team"] = "support" },
            distanceThreshold: 0.6d),
    };

    foreach (var route in routes)
    {
        await router.AddRouteAsync(route, vectorizer);
    }

    // Single best-matching reference.
    var match = await router.RouteAsync("I need a refund on my payment", vectorizer);
    Console.WriteLine(match is null
        ? "No route match."
        : $"Best route: {match.RouteName} (reference '{match.Reference}', distance {match.Distance:F3})");

    // Multiple ranked routes, with each route's references aggregated by the configured method.
    var matches = await router.RouteManyAsync("my package refund is delayed", vectorizer);
    Console.WriteLine($"\nTop {matches.Count} routes for 'my package refund is delayed':");
    foreach (var routeMatch in matches)
    {
        Console.WriteLine($"  {routeMatch.RouteName}: {routeMatch.Distance:F3}");
    }

    // Inspect and grow a route's reference set.
    var billingReferences = await router.GetRouteReferencesAsync("billing");
    Console.WriteLine($"\nbilling references: {string.Join(", ", billingReferences.Select(reference => reference.Reference))}");

    await router.AddRouteReferencesAsync("billing", ["chargeback dispute"], vectorizer);
    Console.WriteLine($"billing references after add: {(await router.GetRouteReferencesAsync("billing")).Count}");

    var removed = await router.DeleteRouteReferencesAsync("billing", ["chargeback dispute"]);
    Console.WriteLine($"removed {removed} billing reference(s)");
}
finally
{
    if (await router.ExistsAsync())
    {
        await router.DropAsync(deleteDocuments: true);
    }
}

// A tiny deterministic bag-of-keywords vectorizer so the example runs without provider credentials.
file sealed class KeywordVectorizer : ITextVectorizer
{
    private static readonly string[] Vocabulary =
    [
        "refund", "billing", "payment", "charge", "dispute",
        "shipping", "delivery", "package", "tracking",
        "password", "login", "account", "support"
    ];

    public static int Dimensions => Vocabulary.Length;

    public Task<float[]> VectorizeAsync(string input, CancellationToken cancellationToken = default)
    {
        var embedding = new float[Vocabulary.Length];
        var tokens = input.ToLowerInvariant().Split(
            [' ', ',', '.', '?', '!'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            var index = Array.IndexOf(Vocabulary, token);
            if (index >= 0)
            {
                embedding[index] += 1f;
            }
        }

        var magnitude = MathF.Sqrt(embedding.Sum(value => value * value));
        if (magnitude > 0f)
        {
            for (var index = 0; index < embedding.Length; index++)
            {
                embedding[index] /= magnitude;
            }
        }

        return Task.FromResult(embedding);
    }
}
