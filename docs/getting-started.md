# Getting Started With Core Index and Document Workflows

This guide walks through the smallest end-to-end `redis-vl-dotnet` flow for v1:

1. add the library to an app
2. connect to Redis Stack or another RediSearch-capable Redis deployment
3. define a schema
4. create an index
5. load documents
6. fetch and query documents

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
var count = await index.CountAsync(new CountQuery(Filter.Tag("genre").Eq("science-fiction")));

Console.WriteLine($"Fetched: {fetched?.Title}");
Console.WriteLine($"Search matches: {results.TotalCount}");
Console.WriteLine($"Genre count: {count}");

foreach (var movie in results.Documents)
{
    Console.WriteLine($"{movie.Title} ({movie.Year})");
}

await index.DropAsync(deleteDocuments: true);

public sealed record Movie(string Id, string Title, int Year, string Genre);
```

## What the Example Does

- `ConnectionMultiplexer.ConnectAsync(...)` creates the Redis connection
- `SearchSchema` defines the index name, key prefix, storage type, and fields
- `SearchIndex.CreateAsync(...)` issues `FT.CREATE`
- `LoadJsonAsync(...)` stores JSON documents under keys derived from the schema prefix and document `Id`
- `FetchJsonByIdAsync<T>(...)` round-trips a typed document from Redis JSON
- `FilterQuery` and `CountQuery` run `FT.SEARCH`-based retrieval and counting through typed query objects

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
- a count query returns `2` for the `science-fiction` genre
- the sample drops the index and loaded documents before exiting

## Switching to HASH Storage

Use `StorageType.Hash` when creating the schema, then switch document lifecycle calls to the HASH-specific methods:

- `LoadHashAsync(...)`
- `FetchHashByIdAsync<T>(...)`
- `FetchHashByKeyAsync<T>(...)`
- `DeleteHashByIdAsync(...)`
- `DeleteHashByKeyAsync(...)`

The query APIs stay the same for JSON and HASH indexes because storage mode is a schema concern, not a separate client type.

## Related Docs

- [v1 architecture and parity matrix](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/v1-architecture.md)
- [testing and local Redis setup](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/testing.md)
- [examples index and prerequisites](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/README.md)
- [runnable JSON storage example](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/JsonStorageExample/README.md)
