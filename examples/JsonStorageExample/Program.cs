using RedisVlDotNet.Filters;
using RedisVlDotNet.Indexes;
using RedisVlDotNet.Queries;
using RedisVlDotNet.Schema;
using StackExchange.Redis;

var redisUrl = Environment.GetEnvironmentVariable("REDIS_VL_REDIS_URL") ?? "localhost:6379";
var schemaPath = Path.Combine(AppContext.BaseDirectory, "schema.yaml");

Console.WriteLine($"Connecting to Redis at {redisUrl}...");
Console.WriteLine($"Loading schema from {schemaPath}...");

using var redis = await ConnectionMultiplexer.ConnectAsync(redisUrl);
var database = redis.GetDatabase();

var schema = SearchSchema.FromYamlFile(schemaPath);

var index = new SearchIndex(database, schema);

await index.CreateAsync(new CreateIndexOptions(overwrite: true, dropExistingDocuments: true));
var indexes = await SearchIndex.ListAsync(database);
var rediscoveredIndex = await SearchIndex.FromExistingAsync(database, schema.Index.Name);

var movies = new[]
{
    new Movie("movie-1", "Heat", 1995, "crime", "A detective and a crew collide in Los Angeles."),
    new Movie("movie-2", "Arrival", 2016, "science-fiction", "A linguist is recruited after alien ships arrive."),
    new Movie("movie-3", "Alien", 1979, "science-fiction", "The Nostromo crew faces a lethal alien organism.")
};

var loadedKeys = await index.LoadJsonAsync(movies.AsEnumerable());
var fetchedMovie = await index.FetchJsonByIdAsync<Movie>("movie-2");
var updatedById = await index.UpdateJsonByIdAsync(
    "movie-2",
    [
        new JsonPartialUpdate("$.summary", "A linguist helps decode the intent of alien visitors."),
        new JsonPartialUpdate("$.year", 2017)
    ]);
var updatedByKey = await index.UpdateJsonByKeyAsync(
    loadedKeys[2],
    [new JsonPartialUpdate("$.title", "Alien (1979)")]);
var updatedMovie = await index.FetchJsonByIdAsync<Movie>("movie-2");

var scienceFictionQuery = new FilterQuery(
    Filter.And(
        Filter.Tag("genre").Eq("science-fiction"),
        Filter.Numeric("year").GreaterThan(1980)),
    returnFields: ["title", "year", "genre"]);

var searchResults = await rediscoveredIndex.SearchAsync<Movie>(scienceFictionQuery);
var projectedTextResults = await rediscoveredIndex.SearchAsync(
    new TextQuery("Alien|Arrival", ["title", "year"], limit: 2));
var typedTextResults = await rediscoveredIndex.SearchAsync<Movie>(
    new TextQuery("Alien|Arrival", ["title", "year", "genre", "summary"], limit: 2));
var batchedTitles = new List<string>();
await foreach (var batch in rediscoveredIndex.SearchBatchesAsync(
    new TextQuery("Alien|Arrival", ["title"], pagination: new QueryPagination(limit: 1)),
    batchSize: 1))
{
    batchedTitles.AddRange(batch.Documents.Select(static document => document.Values["title"].ToString()));
}
var aggregationResults = await rediscoveredIndex.AggregateAsync<GenreSummary>(
    new AggregationQuery(
        groupBy: new AggregationGroupBy(
            ["genre"],
            [
                AggregationReducer.Count("movieCount"),
                AggregationReducer.Average("year", "averageYear")
            ]),
        sortBy: new AggregationSortBy([new AggregationSortField("movieCount", descending: true)]),
        limit: 10));
var scienceFictionCount = await rediscoveredIndex.CountAsync(
    new CountQuery(Filter.Tag("genre").Eq("science-fiction")));
var rediscoveredMovie = await rediscoveredIndex.FetchJsonByIdAsync<Movie>("movie-3");
var clearedCount = await rediscoveredIndex.ClearAsync();

Console.WriteLine($"Loaded keys: {string.Join(", ", loadedKeys)}");
Console.WriteLine($"Fetched by id: {fetchedMovie?.Title} ({fetchedMovie?.Year})");
Console.WriteLine($"Updated movie-2 by id: {updatedById}, now '{updatedMovie?.Title}' ({updatedMovie?.Year})");
Console.WriteLine($"Updated movie-3 by key: {updatedByKey}");
Console.WriteLine($"Available indexes: {string.Join(", ", indexes.Select(static item => item.Name))}");
Console.WriteLine($"Rediscovered index '{rediscoveredIndex.Schema.Index.Name}' with prefixes: {string.Join(", ", rediscoveredIndex.Schema.Index.Prefixes)}");
Console.WriteLine($"Fetched via rediscovered index: {rediscoveredMovie?.Title} ({rediscoveredMovie?.Year})");
Console.WriteLine($"Science fiction count: {scienceFictionCount}");
Console.WriteLine($"Projected text query hits: {projectedTextResults.TotalCount}");
Console.WriteLine($"Typed text query hits: {typedTextResults.TotalCount}");
Console.WriteLine($"Batched text query titles: {string.Join(", ", batchedTitles)}");
Console.WriteLine($"Aggregation rows: {aggregationResults.TotalCount}");
Console.WriteLine($"Cleared indexed documents without dropping the index: {clearedCount}");
Console.WriteLine($"Index metadata: {schema.Index.Prefixes.Count} prefixes, separator '{schema.Index.KeySeparator}', stopwords {(schema.Index.Stopwords?.Count == 0 ? "disabled" : "configured")}.");
Console.WriteLine("Query results:");

foreach (var movie in searchResults.Documents)
{
    Console.WriteLine($"- {movie.Title} ({movie.Year}) [{movie.Genre}]");
}

Console.WriteLine("Projected text query results:");

foreach (var document in projectedTextResults.Documents)
{
    Console.WriteLine($"- {document.Values["title"]} ({document.Values["year"]})");
}

Console.WriteLine("Typed text query results:");

foreach (var movie in typedTextResults.Documents)
{
    Console.WriteLine($"- {movie.Title} ({movie.Year}) [{movie.Genre}]");
}

Console.WriteLine("Aggregation query results:");

foreach (var row in aggregationResults.Rows)
{
    Console.WriteLine($"- {row.Genre}: {row.MovieCount} movies, average year {row.AverageYear:F0}");
}

await index.DropAsync();

Console.WriteLine("Dropped the example index after clearing its documents.");

public sealed record Movie(string Id, string Title, int Year, string Genre, string Summary);

public sealed record GenreSummary(string Genre, int MovieCount, double AverageYear);
