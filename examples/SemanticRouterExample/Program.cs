using RedisVl.Indexes;
using RedisVl.Schema;
using RedisVl.Vectorizers;
using RedisVl.Workflows;
using StackExchange.Redis;

var redisUrl = Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL") ?? "localhost:6379";
await using var connection = await ConnectionMultiplexer.ConnectAsync(redisUrl);
var database = connection.GetDatabase();

var router = new SemanticRouter(
    database,
    new SemanticRouterOptions(
        "semantic-router-example",
        new VectorFieldAttributes(
            VectorAlgorithm.Flat,
            VectorDataType.Float32,
            VectorDistanceMetric.Cosine,
            2),
        distanceThreshold: 0.25d,
        keyNamespace: "examples"));

var vectorizer = new KeywordVectorizer();

try
{
    await router.CreateAsync(new CreateIndexOptions(overwrite: true, dropExistingDocuments: true));

    await router.AddRouteAsync("billing", "refund status", vectorizer);
    await router.AddRouteAsync("shipping", "delivery update", vectorizer);
    await router.AddRouteAsync("support", "technical troubleshooting", vectorizer);

    var match = await router.RouteAsync("where is my refund?", vectorizer);

    Console.WriteLine(match is null
        ? "No route match."
        : $"Route: {match.RouteName}\nReference: {match.Reference}\nDistance: {match.Distance:F3}");
}
finally
{
    if (await router.ExistsAsync())
    {
        await router.DropAsync(deleteDocuments: true);
    }
}

file sealed class KeywordVectorizer : ITextVectorizer
{
    public Task<float[]> VectorizeAsync(string input, CancellationToken cancellationToken = default)
    {
        var normalized = input.ToLowerInvariant();
        var embedding = normalized.Contains("refund", StringComparison.Ordinal)
            ? new[] { 1f, 0f }
            : normalized.Contains("deliver", StringComparison.Ordinal) || normalized.Contains("ship", StringComparison.Ordinal)
                ? new[] { 0f, 1f }
                : new[] { 0.5f, 0.5f };

        return Task.FromResult(embedding);
    }
}
