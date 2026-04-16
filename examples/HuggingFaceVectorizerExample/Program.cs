using RedisVl.Caches;
using RedisVl.Schema;
using RedisVl.Vectorizers.HuggingFace;
using StackExchange.Redis;

var apiKey = Environment.GetEnvironmentVariable("HF_TOKEN");
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("Set HF_TOKEN before running the Hugging Face vectorizer example.");
}

var model = Environment.GetEnvironmentVariable("HF_EMBEDDING_MODEL") ?? "intfloat/multilingual-e5-large";
var redisUrl = Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL") ?? "localhost:6379";

var vectorizer = new HuggingFaceTextVectorizer(
    model,
    apiKey,
    new HuggingFaceVectorizerOptions
    {
        Normalize = true
    });

var dimension = (await vectorizer.VectorizeAsync("dimension probe")).Length;

await using var connection = await ConnectionMultiplexer.ConnectAsync(redisUrl);
var database = connection.GetDatabase();

var cache = new SemanticCache(
    database,
    new SemanticCacheOptions(
        "huggingface-vectorizer-example",
        new VectorFieldAttributes(
            VectorAlgorithm.Flat,
            VectorDataType.Float32,
            VectorDistanceMetric.Cosine,
            dimension),
        distanceThreshold: 0.25d,
        keyNamespace: "examples"));

const string storedPrompt = "How do I rotate an API token?";
const string matchingPrompt = "Need help changing an access token";

try
{
    await cache.CreateAsync();

    var seedEmbeddings = await vectorizer.VectorizeAsync(
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
