using System.Globalization;
using RedisVl.Filters;
using RedisVl.Indexes;
using RedisVl.Queries;
using RedisVl.Schema;
using StackExchange.Redis;

var redisUrl = Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL") ?? "localhost:6379";

Console.WriteLine($"Connecting to Redis at {redisUrl}...");

using var redis = await ConnectionMultiplexer.ConnectAsync(redisUrl);
var database = redis.GetDatabase();

var schema = new SearchSchema(
    new IndexDefinition("vector-search-example-idx", "vector-example:", StorageType.Hash),
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
                efConstruction: 200)),
        new VectorFieldDefinition(
            "poster_embedding",
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

    var updated = await index.UpdateHashByIdAsync(
        "arrival",
        [
            new HashPartialUpdate("summary", "A linguist decodes messages from alien visitors."),
            new HashPartialUpdate("genre", "sci-fi")
        ]);
    var updatedArrival = await index.FetchHashByIdAsync<HashMovie>("arrival");

    var queryVector = new[] { 1f, 0f };
    var vectorQuery = VectorQuery.FromFloat32(
        fieldName: "plot_embedding",
        vector: queryVector,
        topK: 3,
        filter: Filter.Tag("genre").Eq("crime"),
        returnFields: ["title", "summary"],
        scoreAlias: "distance",
        runtimeOptions: new VectorKnnRuntimeOptions(efRuntime: 150),
        pagination: new QueryPagination(limit: 2));
    var secondPageResults = await index.SearchAsync(
        VectorQuery.FromFloat32(
            fieldName: "plot_embedding",
            vector: queryVector,
            topK: 3,
            filter: Filter.Tag("genre").Eq("crime"),
            returnFields: ["title"],
            scoreAlias: "distance",
            pagination: new QueryPagination(offset: 2, limit: 1)));
    var multiVectorResults = await index.SearchAsync(
        new MultiVectorQuery(
            [
                MultiVectorInput.FromFloat32("plot_embedding", queryVector, weight: 0.7),
                MultiVectorInput.FromFloat32("poster_embedding", [0f, 1f], weight: 0.3)
            ],
            topK: 3,
            filter: Filter.Tag("genre").Eq("crime"),
            returnFields: ["title"],
            scoreAlias: "combined_distance",
            runtimeOptions: new VectorKnnRuntimeOptions(efRuntime: 120)));

    var results = await index.SearchAsync(vectorQuery);
    var aggregateHybridResults = await index.AggregateAsync<GenreHybridSummary>(
        AggregateHybridQuery.FromFloat32(
            Filter.Text("title").Prefix("He") | Filter.Text("title").Prefix("Ar"),
            "plot_embedding",
            queryVector,
            topK: 3,
            groupBy: new AggregationGroupBy(
                ["genre"],
                [
                    AggregationReducer.Count("matchCount"),
                    AggregationReducer.Average("vector_distance", "avgDistance")
                ]),
            sortBy: new AggregationSortBy(
                [
                    new AggregationSortField("matchCount", descending: true),
                    new AggregationSortField("avgDistance")
                ]),
            runtimeOptions: new VectorKnnRuntimeOptions(efRuntime: 90)));

    Console.WriteLine($"Updated arrival hash fields: {updated} -> {updatedArrival?.Genre}");
    Console.WriteLine($"Query vector: [{string.Join(", ", queryVector.Select(static value => value.ToString("0.0", CultureInfo.InvariantCulture)))}]");
    Console.WriteLine("Runtime tuning: plot_embedding EF_RUNTIME=150, multi-vector EF_RUNTIME=120, aggregate hybrid EF_RUNTIME=90");
    Console.WriteLine("Nearest neighbors within the crime genre:");

    foreach (var document in results.Documents)
    {
        var title = document.Values["title"];
        var summary = document.Values["summary"];
        var distance = double.Parse(document.Values["distance"]!, CultureInfo.InvariantCulture);

        Console.WriteLine($"- {title} | distance={distance:F6}");
        Console.WriteLine($"  {summary}");
    }

    Console.WriteLine("Weighted multi-vector query results across plot and poster embeddings:");

    foreach (var document in multiVectorResults.Documents)
    {
        var title = document.Values["title"];
        var combinedDistance = double.Parse(document.Values["combined_distance"]!, CultureInfo.InvariantCulture);
        Console.WriteLine($"- {title} | combined_distance={combinedDistance:F6}");
    }

    Console.WriteLine("Aggregate hybrid query results for titles starting with He or Ar:");

    foreach (var row in aggregateHybridResults.Rows)
    {
        Console.WriteLine($"- {row.Genre}: matches={row.MatchCount}, average distance={row.AvgDistance:F6}");
    }

    Console.WriteLine("Second page of the plot_embedding vector search:");

    foreach (var document in secondPageResults.Documents)
    {
        Console.WriteLine($"- {document.Values["title"]}");
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
        "heat",
        [
            new HashEntry("title", "Heat"),
            new HashEntry("genre", "crime"),
            new HashEntry("summary", "A detective and a crew collide in Los Angeles."),
            new HashEntry("plot_embedding", EncodeFloat32([1f, 0f])),
            new HashEntry("poster_embedding", EncodeFloat32([0f, 1f]))
        ]),
    new(
        "thief",
        [
            new HashEntry("title", "Thief"),
            new HashEntry("genre", "crime"),
            new HashEntry("summary", "A professional thief tries one last high-stakes score."),
            new HashEntry("plot_embedding", EncodeFloat32([0.8f, 0.2f])),
            new HashEntry("poster_embedding", EncodeFloat32([0.2f, 0.8f]))
        ]),
    new(
        "heatwave",
        [
            new HashEntry("title", "Heatwave"),
            new HashEntry("genre", "crime"),
            new HashEntry("summary", "A second crew surfaces as a citywide manhunt intensifies."),
            new HashEntry("plot_embedding", EncodeFloat32([0.9f, 0.1f])),
            new HashEntry("poster_embedding", EncodeFloat32([0.1f, 0.9f]))
        ]),
    new(
        "arrival",
        [
            new HashEntry("title", "Arrival"),
            new HashEntry("genre", "science-fiction"),
            new HashEntry("summary", "A linguist is recruited after alien ships arrive."),
            new HashEntry("plot_embedding", EncodeFloat32([0f, 1f])),
            new HashEntry("poster_embedding", EncodeFloat32([1f, 0f]))
        ])
];

static byte[] EncodeFloat32(float[] vector)
{
    var bytes = new byte[vector.Length * sizeof(float)];
    Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
    return bytes;
}

internal sealed record HashSeedDocument(string Id, HashEntry[] Entries);
internal sealed record HashMovie(string Id, string Title, string Genre, string Summary);
internal sealed record GenreHybridSummary(string Genre, int MatchCount, double AvgDistance);
