using System.Globalization;
using RedisVL.Caches;
using RedisVL.Schema;
using RedisVL.Vectorizers.Cohere;
using StackExchange.Redis;

var apiKey = Environment.GetEnvironmentVariable("COHERE_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("Set COHERE_API_KEY before running the Cohere vectorizer example.");
}

var model = Environment.GetEnvironmentVariable("COHERE_EMBEDDING_MODEL") ?? "embed-english-v3.0";
var redisUrl = Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL") ?? "localhost:6379";

// Cohere distinguishes between embedding documents you store and queries you search with.
// Use SearchDocument when seeding the cache and SearchQuery when checking it.
var documentVectorizer = new CohereTextVectorizer(
    model,
    apiKey,
    new CohereVectorizerOptions
    {
        InputType = CohereInputType.SearchDocument,
        ClientName = "redis-vl-dotnet-example"
    });

var queryVectorizer = new CohereTextVectorizer(
    model,
    apiKey,
    new CohereVectorizerOptions
    {
        InputType = CohereInputType.SearchQuery,
        ClientName = "redis-vl-dotnet-example"
    });

var dimension = (await documentVectorizer.VectorizeAsync("dimension probe")).Length;
Console.WriteLine($"Cohere model '{model}' produces {dimension}-dimensional embeddings.");

await using var connection = await ConnectionMultiplexer.ConnectAsync(redisUrl);
var database = connection.GetDatabase();

// Cohere v3 embeddings are asymmetric: a `search_query` vector is compared against a
// `search_document` vector, and a strong match still lands at a larger cosine distance than
// you'd see from a normalized OpenAI/Hugging Face model (typically ~0.5-0.65 for a good match).
// We retrieve the nearest entry regardless of distance, then classify hit vs. miss in app code
// against matchThreshold so the actual distance is always printed and easy to tune.
// Override the cutoff with COHERE_MATCH_THRESHOLD without recompiling.
var matchThreshold = double.TryParse(
    Environment.GetEnvironmentVariable("COHERE_MATCH_THRESHOLD"),
    NumberStyles.Float,
    CultureInfo.InvariantCulture,
    out var parsedThreshold)
    ? parsedThreshold
    : 0.7d;

var cache = new SemanticCache(
    database,
    new SemanticCacheOptions(
        "cohere-vectorizer-example",
        new VectorFieldAttributes(
            VectorAlgorithm.Flat,
            VectorDataType.Float32,
            VectorDistanceMetric.Cosine,
            dimension),
        distanceThreshold: 2.0d, // cosine distance max — retrieve the nearest entry no matter what
        keyNamespace: "examples"));

const string storedPrompt = "How do I rotate an API token?";
const string matchingPrompt = "Need help changing an access token";

try
{
    await cache.CreateAsync();

    var seedEmbeddings = await documentVectorizer.VectorizeAsync(
        [
            storedPrompt,
            "How do I export billing history?"
        ]);

    await cache.StoreAsync(
        storedPrompt,
        "Open Settings > Access > Tokens, create a replacement token, then revoke the old one.",
        seedEmbeddings[0],
        metadata: new
        {
            source = "faq",
            model
        });

    var matches = await cache.CheckTopKAsync(matchingPrompt, queryVectorizer, topK: 1);
    if (matches.Count == 0)
    {
        Console.WriteLine("Cache is empty — nothing to match against.");
    }
    else
    {
        var nearest = matches[0];
        Console.WriteLine($"Nearest stored prompt: \"{nearest.Prompt}\" (distance {nearest.Distance:F4})");
        if (nearest.Distance <= matchThreshold)
        {
            Console.WriteLine($"Hit (within {matchThreshold:F2}): {nearest.Response}\nMetadata: {nearest.Metadata}");
        }
        else
        {
            Console.WriteLine(
                $"No hit — nearest distance {nearest.Distance:F4} exceeds the match threshold {matchThreshold:F2}. " +
                "Raise matchThreshold (or try a closer query) to accept it.");
        }
    }
}
finally
{
    if (await cache.ExistsAsync())
    {
        await cache.DropAsync(deleteDocuments: true);
    }
}
