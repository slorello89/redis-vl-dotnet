using System.Globalization;
using RedisVL.Indexes;
using RedisVL.Queries;
using RedisVL.Schema;
using StackExchange.Redis;

var redisUrl = Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL") ?? "localhost:6379";

Console.WriteLine($"Connecting to Redis at {redisUrl}...");

using var redis = await ConnectionMultiplexer.ConnectAsync(redisUrl);
var database = redis.GetDatabase();

// SVS-VAMANA is a graph-based vector index that supports vector compression
// (LVQ / LeanVec) to reduce memory usage. It requires Redis 8.x or newer.
var schema = new SearchSchema(
    new IndexDefinition("svs-vamana-example-idx", "svs-example:", StorageType.Hash),
    [
        new TagFieldDefinition("category"),
        new TextFieldDefinition("title"),
        new VectorFieldDefinition(
            "embedding",
            new VectorFieldAttributes(
                VectorAlgorithm.SvsVamana,
                VectorDataType.Float32,
                VectorDistanceMetric.Cosine,
                dimensions: 4,
                // Build-time SVS-VAMANA knobs.
                compression: VectorCompression.Lvq8,
                constructionWindowSize: 200,
                graphMaxDegree: 32,
                searchWindowSize: 10))
    ]);

var index = new SearchIndex(database, schema);

try
{
    await index.CreateAsync(new CreateIndexOptions(overwrite: true, dropExistingDocuments: true));

    await SeedDocumentsAsync(database, schema);
    await WaitForDocumentCountAsync(index, expectedCount: 4);

    var queryVector = new[] { 0.9f, 0.1f, 0f, 0f };

    // KNN query with SVS-VAMANA query-time runtime knobs. These map onto the
    // SEARCH_WINDOW_SIZE / USE_SEARCH_HISTORY / SEARCH_BUFFER_CAPACITY params.
    var knnResults = await index.SearchAsync(
        VectorQuery.FromFloat32(
            fieldName: "embedding",
            vector: queryVector,
            topK: 3,
            returnFields: ["title", "category"],
            scoreAlias: "distance",
            runtimeOptions: new VectorKnnRuntimeOptions(
                searchWindowSize: 40,
                useSearchHistory: SvsSearchHistory.On,
                searchBufferCapacity: 80)));

    // Range query with epsilon. SVS-VAMANA supports the epsilon range knob too.
    var rangeResults = await index.SearchAsync(
        VectorRangeQuery.FromFloat32(
            fieldName: "embedding",
            vector: queryVector,
            distanceThreshold: 0.4,
            returnFields: ["title"],
            scoreAlias: "distance",
            runtimeOptions: new VectorRangeRuntimeOptions(epsilon: 0.05)));

    Console.WriteLine($"Query vector: [{string.Join(", ", queryVector.Select(static value => value.ToString("0.0#", CultureInfo.InvariantCulture)))}]");
    Console.WriteLine("Index: SVS-VAMANA with LVQ8 compression (graph_max_degree=32, construction_window_size=200)");
    Console.WriteLine("KNN runtime tuning: SEARCH_WINDOW_SIZE=40, USE_SEARCH_HISTORY=ON, SEARCH_BUFFER_CAPACITY=80");
    Console.WriteLine("Nearest neighbors:");

    foreach (var document in knnResults.Documents)
    {
        var title = document.Values["title"];
        var category = document.Values["category"];
        var distance = double.Parse(document.Values["distance"]!, CultureInfo.InvariantCulture);
        Console.WriteLine($"- {title} [{category}] | distance={distance:F6}");
    }

    Console.WriteLine("Range query results within distance 0.4 (epsilon=0.05):");

    foreach (var document in rangeResults.Documents)
    {
        var title = document.Values["title"];
        var distance = double.Parse(document.Values["distance"]!, CultureInfo.InvariantCulture);
        Console.WriteLine($"- {title} | distance={distance:F6}");
    }
}
finally
{
    if (await index.ExistsAsync())
    {
        await index.DropAsync(deleteDocuments: true);
    }
}

Console.WriteLine("Cleaned up example index and documents.");

static async Task SeedDocumentsAsync(IDatabase database, SearchSchema schema)
{
    foreach (var document in CreateSeedDocuments())
    {
        await database.HashSetAsync($"{schema.Index.Prefix}{document.Id}", document.Entries);
    }
}

static async Task WaitForDocumentCountAsync(SearchIndex index, int expectedCount)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

    try
    {
        while (!timeout.Token.IsCancellationRequested)
        {
            var info = await index.InfoAsync(timeout.Token);
            var indexedCount = info.GetString("num_docs");

            if (double.TryParse(indexedCount, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedCount) &&
                parsedCount >= expectedCount)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }
    }
    catch (OperationCanceledException) when (timeout.IsCancellationRequested)
    {
        throw new TimeoutException("Timed out waiting for the example documents to be indexed.");
    }
}

static IReadOnlyList<HashSeedDocument> CreateSeedDocuments() =>
[
    new(
        "alpha",
        [
            new HashEntry("title", "Alpha"),
            new HashEntry("category", "primary"),
            new HashEntry("embedding", EncodeFloat32([1f, 0f, 0f, 0f]))
        ]),
    new(
        "beta",
        [
            new HashEntry("title", "Beta"),
            new HashEntry("category", "primary"),
            new HashEntry("embedding", EncodeFloat32([0.8f, 0.2f, 0f, 0f]))
        ]),
    new(
        "gamma",
        [
            new HashEntry("title", "Gamma"),
            new HashEntry("category", "secondary"),
            new HashEntry("embedding", EncodeFloat32([0f, 1f, 0f, 0f]))
        ]),
    new(
        "delta",
        [
            new HashEntry("title", "Delta"),
            new HashEntry("category", "secondary"),
            new HashEntry("embedding", EncodeFloat32([0f, 0f, 1f, 0f]))
        ])
];

static byte[] EncodeFloat32(float[] vector)
{
    var bytes = new byte[vector.Length * sizeof(float)];
    Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
    return bytes;
}

internal sealed record HashSeedDocument(string Id, HashEntry[] Entries);
