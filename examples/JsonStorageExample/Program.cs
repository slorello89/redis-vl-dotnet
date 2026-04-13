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
    new IndexDefinition(
        "json-storage-example-idx",
        ["json-example:", "json-example-archive:"],
        StorageType.Json,
        keySeparator: '|',
        stopwords: []),
    [
        new TextFieldDefinition("title"),
        new NumericFieldDefinition("year"),
        new TagFieldDefinition("genre"),
        new TextFieldDefinition("summary")
    ]);

var index = new SearchIndex(database, schema);

await index.CreateAsync(new CreateIndexOptions(overwrite: true, dropExistingDocuments: true));

var movies = new[]
{
    new Movie("movie-1", "Heat", 1995, "crime", "A detective and a crew collide in Los Angeles."),
    new Movie("movie-2", "Arrival", 2016, "science-fiction", "A linguist is recruited after alien ships arrive."),
    new Movie("movie-3", "Alien", 1979, "science-fiction", "The Nostromo crew faces a lethal alien organism.")
};

var loadedKeys = await index.LoadJsonAsync(movies);
var fetchedMovie = await index.FetchJsonByIdAsync<Movie>("movie-2");

var scienceFictionQuery = new FilterQuery(
    Filter.And(
        Filter.Tag("genre").Eq("science-fiction"),
        Filter.Numeric("year").GreaterThan(1980)),
    returnFields: ["title", "year", "genre"]);

var searchResults = await index.SearchAsync<Movie>(scienceFictionQuery);
var scienceFictionCount = await index.CountAsync(
    new CountQuery(Filter.Tag("genre").Eq("science-fiction")));

Console.WriteLine($"Loaded keys: {string.Join(", ", loadedKeys)}");
Console.WriteLine($"Fetched by id: {fetchedMovie?.Title} ({fetchedMovie?.Year})");
Console.WriteLine($"Science fiction count: {scienceFictionCount}");
Console.WriteLine($"Index metadata: separator '{schema.Index.KeySeparator}', stopwords disabled.");
Console.WriteLine("Query results:");

foreach (var movie in searchResults.Documents)
{
    Console.WriteLine($"- {movie.Title} ({movie.Year}) [{movie.Genre}]");
}

await index.DropAsync(deleteDocuments: true);

Console.WriteLine("Cleaned up example index and documents.");

public sealed record Movie(string Id, string Title, int Year, string Genre, string Summary);
