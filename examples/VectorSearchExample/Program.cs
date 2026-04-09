using System.Globalization;
using RedisVlDotNet.Filters;
using RedisVlDotNet.Indexes;
using RedisVlDotNet.Queries;
using RedisVlDotNet.Schema;
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
            "embedding",
            new VectorFieldAttributes(
                VectorAlgorithm.Flat,
                VectorDataType.Float32,
                VectorDistanceMetric.Cosine,
                dimensions: 2))
    ]);

var index = new SearchIndex(database, schema);

try
{
    await index.CreateAsync(new CreateIndexOptions(overwrite: true, dropExistingDocuments: true));

    await SeedDocumentsAsync(database, schema);
    await WaitForDocumentCountAsync(index, expectedCount: 3);

    var queryVector = new[] { 1f, 0f };
    var vectorQuery = VectorQuery.FromFloat32(
        fieldName: "embedding",
        vector: queryVector,
        topK: 3,
        filter: Filter.Tag("genre").Eq("crime"),
        returnFields: ["title", "summary"],
        scoreAlias: "distance");

    var results = await index.SearchAsync(vectorQuery);

    Console.WriteLine($"Query vector: [{string.Join(", ", queryVector.Select(static value => value.ToString("0.0", CultureInfo.InvariantCulture)))}]");
    Console.WriteLine("Nearest neighbors within the crime genre:");

    foreach (var document in results.Documents)
    {
        var title = document.Values["title"];
        var summary = document.Values["summary"];
        var distance = double.Parse(document.Values["distance"]!, CultureInfo.InvariantCulture);

        Console.WriteLine($"- {title} | distance={distance:F6}");
        Console.WriteLine($"  {summary}");
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
            new HashEntry("embedding", EncodeFloat32([1f, 0f]))
        ]),
    new(
        "thief",
        [
            new HashEntry("title", "Thief"),
            new HashEntry("genre", "crime"),
            new HashEntry("summary", "A professional thief tries one last high-stakes score."),
            new HashEntry("embedding", EncodeFloat32([0.8f, 0.2f]))
        ]),
    new(
        "arrival",
        [
            new HashEntry("title", "Arrival"),
            new HashEntry("genre", "science-fiction"),
            new HashEntry("summary", "A linguist is recruited after alien ships arrive."),
            new HashEntry("embedding", EncodeFloat32([0f, 1f]))
        ])
];

static byte[] EncodeFloat32(float[] vector)
{
    var bytes = new byte[vector.Length * sizeof(float)];
    Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
    return bytes;
}

internal sealed record HashSeedDocument(string Id, HashEntry[] Entries);
