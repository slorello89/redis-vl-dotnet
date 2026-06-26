using System.Globalization;
using RedisVL.Filters;
using RedisVL.Indexes;
using RedisVL.Queries;
using RedisVL.Schema;
using StackExchange.Redis;

// HybridSearchQuery issues a native FT.HYBRID command (Redis 8.4+). The text branch (SEARCH)
// and the vector branch (VSIM) are scored independently and fused server-side, so this example
// requires a Redis 8.4+ server. On older servers use HybridQuery or AggregateHybridQuery instead.
var redisUrl = Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL") ?? "localhost:6379";

Console.WriteLine($"Connecting to Redis at {redisUrl}...");

using var redis = await ConnectionMultiplexer.ConnectAsync(redisUrl);
var database = redis.GetDatabase();

var schema = new SearchSchema(
    new IndexDefinition("hybrid-search-example-idx", "hybrid-example:", StorageType.Hash),
    [
        new TagFieldDefinition("genre"),
        new TextFieldDefinition("title"),
        new TextFieldDefinition("summary"),
        new VectorFieldDefinition(
            "plot_embedding",
            new VectorFieldAttributes(
                VectorAlgorithm.Hnsw,
                VectorDataType.Float32,
                VectorDistanceMetric.Cosine,
                dimensions: 2,
                m: 16,
                efConstruction: 200))
    ]);

var index = new SearchIndex(database, schema);

try
{
    await index.CreateAsync(new CreateIndexOptions(overwrite: true, dropExistingDocuments: true));

    await SeedDocumentsAsync(database, schema);
    await WaitForDocumentCountAsync(index, expectedCount: 4);

    // The text query drives the lexical SEARCH branch and must contain at least one text predicate.
    // "He*" lexically matches "Heat" and "Heatwave"; the [1, 0] vector is an exact match for "Heat".
    var textQuery = Filter.Text("title").Prefix("He");
    var queryVector = new[] { 1f, 0f };

    // 1. Linear fusion: weight the lexical (alpha) and vector (beta) scores explicitly.
    var linearResults = await index.SearchAsync(
        HybridSearchQuery.FromFloat32(
            textQuery,
            "plot_embedding",
            queryVector,
            topK: 3,
            combination: new LinearHybridCombination(alpha: 0.7, beta: 0.3),
            returnFields: ["title", "genre"],
            runtimeOptions: new VectorKnnRuntimeOptions(efRuntime: 150)));

    // 2. Server-default fusion: passing no combination uses RRF (window 20, constant 60).
    var defaultResults = await index.SearchAsync(
        HybridSearchQuery.FromFloat32(
            textQuery,
            "plot_embedding",
            queryVector,
            topK: 3,
            returnFields: ["title", "genre"]));

    // 3. Reciprocal Rank Fusion with an explicit constant and window.
    var rrfResults = await index.SearchAsync(
        HybridSearchQuery.FromFloat32(
            textQuery,
            "plot_embedding",
            queryVector,
            topK: 3,
            combination: new ReciprocalRankFusionHybridCombination(constant: 60, window: 20),
            returnFields: ["title", "genre"]));

    // 4. Vector pre-filter: restrict the VSIM candidate set to the crime genre before fusion.
    //    Note this only constrains the vector branch — lexical "He*" matches in other genres
    //    (e.g. the science-fiction "Helios") can still surface through the SEARCH branch.
    var filteredResults = await index.SearchAsync(
        HybridSearchQuery.FromFloat32(
            textQuery,
            "plot_embedding",
            queryVector,
            topK: 3,
            combination: new LinearHybridCombination(alpha: 0.5, beta: 0.5),
            vectorFilter: Filter.Tag("genre").Eq("crime"),
            returnFields: ["title", "genre"]));

    // 5. Typed mapping: project each result onto a record.
    var typedResults = await index.SearchAsync<HybridMovie>(
        HybridSearchQuery.FromFloat32(
            textQuery,
            "plot_embedding",
            queryVector,
            topK: 3,
            combination: new LinearHybridCombination(alpha: 0.7, beta: 0.3),
            returnFields: ["title", "genre", "summary"]));

    Console.WriteLine($"Query vector: [{string.Join(", ", queryVector.Select(static value => value.ToString("0.0", CultureInfo.InvariantCulture)))}]");
    Console.WriteLine("Text branch: title starts with \"He\"");
    Console.WriteLine();

    PrintFusedResults("1. Linear fusion (alpha=0.7, beta=0.3, EF_RUNTIME=150)", linearResults);
    PrintFusedResults("2. Server-default fusion (RRF window=20, constant=60)", defaultResults);
    PrintFusedResults("3. Explicit RRF (constant=60, window=20)", rrfResults);
    PrintFusedResults("4. Linear fusion with a crime-genre vector pre-filter (vector branch only)", filteredResults);

    Console.WriteLine("5. Typed mapping onto HybridMovie:");
    foreach (var movie in typedResults.Documents)
    {
        Console.WriteLine($"- {movie.Id} | {movie.Title} [{movie.Genre}]");
        Console.WriteLine($"  {movie.Summary}");
    }
}
finally
{
    if (await index.ExistsAsync())
    {
        await index.DropAsync(deleteDocuments: true);
    }
}

Console.WriteLine();
Console.WriteLine("Cleaned up example index and documents.");

// Each FT.HYBRID result carries the source document key as its Id and the fused score under
// HybridSearchQuery.ScoreField ("__score"). The key field itself is not surfaced in Values.
static void PrintFusedResults(string heading, SearchResults results)
{
    Console.WriteLine(heading + ":");

    foreach (var document in results.Documents)
    {
        var title = document.Values["title"];
        var genre = document.Values["genre"];
        var score = double.TryParse(
            document.Values[HybridSearchQuery.ScoreField],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsedScore)
            ? parsedScore.ToString("F6", CultureInfo.InvariantCulture)
            : "n/a";

        Console.WriteLine($"- {document.Id} | {title} [{genre}] | score={score}");
    }

    Console.WriteLine();
}

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
        "heat",
        [
            new HashEntry("title", "Heat"),
            new HashEntry("genre", "crime"),
            new HashEntry("summary", "A detective and a crew collide in Los Angeles."),
            new HashEntry("plot_embedding", EncodeFloat32([1f, 0f]))
        ]),
    new(
        "heatwave",
        [
            new HashEntry("title", "Heatwave"),
            new HashEntry("genre", "crime"),
            new HashEntry("summary", "A second crew surfaces as a citywide manhunt intensifies."),
            new HashEntry("plot_embedding", EncodeFloat32([0.9f, 0.1f]))
        ]),
    new(
        "helios",
        [
            new HashEntry("title", "Helios"),
            new HashEntry("genre", "science-fiction"),
            new HashEntry("summary", "A solar mission drifts off course near the sun."),
            new HashEntry("plot_embedding", EncodeFloat32([0.2f, 0.8f]))
        ]),
    new(
        "thief",
        [
            new HashEntry("title", "Thief"),
            new HashEntry("genre", "crime"),
            new HashEntry("summary", "A professional thief tries one last high-stakes score."),
            new HashEntry("plot_embedding", EncodeFloat32([0.8f, 0.2f]))
        ])
];

static byte[] EncodeFloat32(float[] vector)
{
    var bytes = new byte[vector.Length * sizeof(float)];
    Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
    return bytes;
}

internal sealed record HashSeedDocument(string Id, HashEntry[] Entries);
internal sealed record HybridMovie(string Id, string Title, string Genre, string Summary);
