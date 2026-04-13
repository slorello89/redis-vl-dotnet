using RedisVlDotNet.Caches;
using RedisVlDotNet.Schema;
using RedisVlDotNet.Vectorizers.OpenAI;
using StackExchange.Redis;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("Set OPENAI_API_KEY before running the OpenAI vectorizer example.");
}

var model = Environment.GetEnvironmentVariable("OPENAI_EMBEDDING_MODEL") ?? "text-embedding-3-small";
var dimensions = ParseDimensions(Environment.GetEnvironmentVariable("OPENAI_EMBEDDING_DIMENSIONS")) ?? 256;
var redisUrl = Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL") ?? "localhost:6379";

var vectorizer = new OpenAiTextVectorizer(
    model,
    apiKey,
    new OpenAiVectorizerOptions
    {
        Dimensions = dimensions,
        EndUserId = "redis-vl-dotnet-example"
    });

await using var connection = await ConnectionMultiplexer.ConnectAsync(redisUrl);
var database = connection.GetDatabase();

var cache = new SemanticCache(
    database,
    new SemanticCacheOptions(
        "openai-vectorizer-example",
        new VectorFieldAttributes(
            VectorAlgorithm.Flat,
            VectorDataType.Float32,
            VectorDistanceMetric.Cosine,
            dimensions),
        distanceThreshold: 0.35d,
        keyNamespace: "examples"));

const string storedPrompt = "How do I reset my password?";
const string matchingPrompt = "Need help resetting my password";

try
{
    await cache.CreateAsync();

    var seedEmbeddings = await vectorizer.VectorizeAsync(
        [
            storedPrompt,
            "Where can I download my invoices?"
        ]);

    await cache.StoreAsync(
        storedPrompt,
        "Open Settings > Security > Reset password and follow the email link.",
        seedEmbeddings[0],
        metadata: new
        {
            source = "faq",
            model
        });

    var hit = await cache.CheckAsync(matchingPrompt, vectorizer);
    Console.WriteLine(hit is null
        ? "No semantic cache hit."
        : $"Hit: {hit.Response}\nMetadata: {hit.Metadata}\nDistance: {hit.Distance:F4}");
}
finally
{
    if (await cache.ExistsAsync())
    {
        await cache.DropAsync(deleteDocuments: true);
    }
}

static int? ParseDimensions(string? rawValue)
{
    if (string.IsNullOrWhiteSpace(rawValue))
    {
        return null;
    }

    return int.Parse(rawValue, System.Globalization.CultureInfo.InvariantCulture);
}
