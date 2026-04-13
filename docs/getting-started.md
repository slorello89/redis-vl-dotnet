# Getting Started With Core Index and Document Workflows

This guide walks through the smallest end-to-end `redis-vl-dotnet` flow for v1:

1. add the library to an app
2. connect to Redis Stack or another RediSearch-capable Redis deployment
3. define a schema
4. create an index
5. load documents
6. fetch and query documents
7. run full-text search with `TextQuery`
8. run grouped aggregations with typed reducer output
9. optionally clear indexed documents without dropping the index

The current repository does not publish a NuGet package yet, so local development uses a project reference.

## Prerequisites

- .NET 9 SDK
- Redis Stack or another Redis deployment with RediSearch and RedisJSON enabled
- A reachable Redis connection string such as `localhost:6379`

Start Redis Stack locally with the repository's compose file:

```bash
docker compose -f docker-compose.integration.yml up -d redis-stack
```

## Add the Library to an App

Create a console app:

```bash
dotnet new console -n RedisVlGettingStarted
cd RedisVlGettingStarted
dotnet add package StackExchange.Redis
dotnet add reference ../redis-vl-dotnet/src/RedisVlDotNet/RedisVlDotNet.csproj
```

If `redis-vl-dotnet` is later published as a NuGet package, replace the project reference with:

```bash
dotnet add package RedisVlDotNet
```

## Create a Minimal JSON Workflow

Replace `Program.cs` with:

```csharp
using RedisVlDotNet.Filters;
using RedisVlDotNet.Indexes;
using RedisVlDotNet.Queries;
using RedisVlDotNet.Schema;
using StackExchange.Redis;

var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var database = redis.GetDatabase();

var schema = new SearchSchema(
    new IndexDefinition("movies-idx", "movie:", StorageType.Json),
    [
        new TextFieldDefinition("title"),
        new NumericFieldDefinition("year"),
        new TagFieldDefinition("genre")
    ]);

var index = new SearchIndex(database, schema);

await index.CreateAsync(new CreateIndexOptions(skipIfExists: true));

await index.LoadJsonAsync(
    [
        new Movie("movie-1", "Heat", 1995, "crime"),
        new Movie("movie-2", "Arrival", 2016, "science-fiction"),
        new Movie("movie-3", "Alien", 1979, "science-fiction")
    ]);

var fetched = await index.FetchJsonByIdAsync<Movie>("movie-1");

var query = new FilterQuery(
    Filter.And(
        Filter.Tag("genre").Eq("science-fiction"),
        Filter.Numeric("year").GreaterThan(1980)),
    returnFields: ["title", "year", "genre"]);

var results = await index.SearchAsync<Movie>(query);
var textResults = await index.SearchAsync<Movie>(
    new TextQuery("Alien|Arrival", ["title", "year", "genre"], limit: 2));
var aggregationResults = await index.AggregateAsync<GenreSummary>(
    new AggregationQuery(
        groupBy: new AggregationGroupBy(
            ["genre"],
            [
                AggregationReducer.Count("movieCount"),
                AggregationReducer.Average("year", "averageYear")
            ]),
        sortBy: new AggregationSortBy([new AggregationSortField("movieCount", descending: true)]),
        limit: 10));
var count = await index.CountAsync(new CountQuery(Filter.Tag("genre").Eq("science-fiction")));
var cleared = await index.ClearAsync();

Console.WriteLine($"Fetched: {fetched?.Title}");
Console.WriteLine($"Search matches: {results.TotalCount}");
Console.WriteLine($"Text matches: {textResults.TotalCount}");
Console.WriteLine($"Aggregation rows: {aggregationResults.TotalCount}");
Console.WriteLine($"Genre count: {count}");
Console.WriteLine($"Cleared documents: {cleared}");

foreach (var movie in results.Documents)
{
    Console.WriteLine($"{movie.Title} ({movie.Year})");
}

foreach (var movie in textResults.Documents)
{
    Console.WriteLine($"Text search: {movie.Title} ({movie.Year})");
}

foreach (var row in aggregationResults.Rows)
{
    Console.WriteLine($"Aggregation: {row.Genre} => {row.MovieCount} movies, average year {row.AverageYear:F0}");
}

await index.DropAsync();

public sealed record Movie(string Id, string Title, int Year, string Genre);
public sealed record GenreSummary(string Genre, int MovieCount, double AverageYear);
```

## What the Example Does

- `ConnectionMultiplexer.ConnectAsync(...)` creates the Redis connection
- `SearchSchema` defines the index name, key prefix, storage type, and fields
- `SearchIndex.CreateAsync(...)` issues `FT.CREATE`
- `LoadJsonAsync(...)` stores JSON documents under keys derived from the schema prefix and document `Id`
- `FetchJsonByIdAsync<T>(...)` round-trips a typed document from Redis JSON
- `FilterQuery`, `TextQuery`, and `CountQuery` run `FT.SEARCH`-based retrieval and counting through typed query objects
- `AggregateAsync<T>(...)` runs `FT.AGGREGATE` and maps grouped rows into typed reducer projections
- `ClearAsync(...)` deletes Redis keys matched by the schema prefixes and preserves the index definition for both JSON and HASH storage

## Run Full-Text Search With `TextQuery`

Use `TextQuery` when you want RediSearch text matching semantics instead of the structured filter builder:

```csharp
var textResults = await index.SearchAsync<Movie>(
    new TextQuery("Alien|Arrival", ["title", "year", "genre"], limit: 2));
```

`TextQuery` keeps the same paging and return-field conventions as the other query types:

- the first argument is the raw RediSearch query string
- `returnFields` limits the projected fields returned from Redis
- `SearchAsync<T>(...)` maps those projected fields into a typed result document
- `QueryPagination` provides the shared paging model across text, filter, vector, hybrid, aggregation, and vector-range queries

```csharp
var pagedVectorResults = await index.SearchAsync(
    VectorQuery.FromFloat32(
        "embedding",
        [1f, 0f],
        topK: 10,
        returnFields: ["title"],
        pagination: new QueryPagination(offset: 3, limit: 3)));
```

When you want to consume every page without writing the continuation loop yourself, use the batch helpers:

```csharp
await foreach (var batch in index.SearchBatchesAsync(
    new TextQuery("Alien|Arrival", ["title"], pagination: new QueryPagination(limit: 1)),
    batchSize: 1))
{
    foreach (var document in batch.Documents)
    {
        Console.WriteLine(document.Values["title"]);
    }
}
```

## Run Aggregations With Typed Results

Use `AggregationQuery` when you want RediSearch grouping and reducer pipelines without parsing raw Redis arrays yourself:

```csharp
var aggregationResults = await index.AggregateAsync<GenreSummary>(
    new AggregationQuery(
        groupBy: new AggregationGroupBy(
            ["genre"],
            [
                AggregationReducer.Count("movieCount"),
                AggregationReducer.Average("year", "averageYear")
            ]),
        sortBy: new AggregationSortBy([new AggregationSortField("movieCount", descending: true)]),
        limit: 10));
```

`AggregationQuery` uses the RediSearch aggregation pipeline concepts directly:

- `groupBy` defines the grouping keys and reducer functions
- reducer aliases such as `movieCount` and `averageYear` become the field names used for typed mapping
- `AggregateAsync(...)` returns parsed `AggregationResults`, and `AggregateAsync<T>(...)` maps each row into your projection type

## Run Aggregate Hybrid Queries

Use `AggregateHybridQuery` on vector-enabled schemas when you want KNN retrieval and aggregation in the same `FT.AGGREGATE` pipeline:

```csharp
var hybridResults = await index.AggregateAsync<GenreHybridSummary>(
    AggregateHybridQuery.FromFloat32(
        Filter.Text("title").Prefix("He") | Filter.Text("title").Prefix("Ar"),
        "embedding",
        [1f, 0f],
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
            ])));
```

`AggregateHybridQuery` keeps the aggregation-stage model from `AggregationQuery`, while adding the hybrid vector inputs:

- `textFilter` must contain at least one text predicate so RediSearch can constrain the hybrid text side of the query
- `vectorFieldName`, `vector`, `topK`, and `scoreAlias` define the KNN portion of the aggregate query
- reducer aliases can reference the hybrid distance alias, for example `AggregationReducer.Average("vector_distance", "avgDistance")`

## Run the Flow Locally

From the console app directory:

```bash
dotnet run
```

Expected behavior:

- the index is created if it does not already exist
- three JSON documents are loaded under the `movie:` prefix
- one document is fetched directly by id
- a filtered search returns `Arrival`
- a `TextQuery` returns `Arrival` and `Alien`
- an aggregation groups the results by genre and returns typed reducer rows
- a count query returns `2` for the `science-fiction` genre
- the sample clears the prefixed documents, then drops the now-empty index before exiting

## Switching to HASH Storage

Use `StorageType.Hash` when creating the schema, then switch document lifecycle calls to the HASH-specific methods:

- `LoadHashAsync(...)`
- `FetchHashByIdAsync<T>(...)`
- `FetchHashByKeyAsync<T>(...)`
- `DeleteHashByIdAsync(...)`
- `DeleteHashByKeyAsync(...)`

The query APIs stay the same for JSON and HASH indexes because storage mode is a schema concern, not a separate client type.

`ClearAsync(...)` is storage-agnostic at the API level but operates on Redis keys, not document payload internals:

- for `StorageType.Json`, it deletes JSON document keys that match the schema prefixes
- for `StorageType.Hash`, it deletes HASH document keys that match the schema prefixes

Use narrow, index-specific prefixes so clearing one index does not remove unrelated application keys.

## Related Docs

- [current parity roadmap and matrix](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/parity-roadmap.md)
- [testing and local Redis setup](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/testing.md)
- [examples index and prerequisites](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/README.md)
- [runnable JSON storage example](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/JsonStorageExample/README.md)
