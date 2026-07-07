# redis-vl-dotnet

[![NuGet](https://img.shields.io/nuget/v/RedisVL.svg)](https://www.nuget.org/packages/RedisVL/)

`redis-vl-dotnet` is a .NET-native Redis Vector Library for Redis Search and vector workloads. It gives you a fluent, strongly-typed API over [Redis Query Engine](https://redis.io/docs/latest/develop/interact/search-and-query/) so you can define schemas, index HASH or JSON documents, and run vector, full-text, filter, hybrid, and aggregation queries — plus higher-level building blocks for GenAI apps such as semantic caching, semantic routing, embeddings caching, and semantic message history.

It is the .NET sibling of [redis-vl-python](https://github.com/redis/redis-vl-python), built on top of [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis).

## Features

- **Schema and index management** — define indexes in code or load them from YAML, create/drop/list indexes, and reconnect to existing ones.
- **Document storage** — store and fetch strongly-typed records over HASH or JSON, with partial updates.
- **Query types** — nearest-neighbor `VectorQuery`, `MultiVectorQuery`, vector range queries, `FilterQuery`, `TextQuery`, `AggregationQuery`, and native `FT.HYBRID` `HybridSearchQuery` (Redis 8.4+).
- **Fluent filters** — compose tag, text, numeric, and geo predicates with `&`, `|`, and negation.
- **Vector indexing options** — FLAT, HNSW, and SVS-VAMANA algorithms, with LVQ / LeanVec compression.
- **GenAI extensions** — `SemanticCache`, `SemanticRouter`, `EmbeddingsCache`, and semantic message history.
- **Pluggable vectorizers and rerankers** — OpenAI, Cohere, Hugging Face, local ONNX, and `Microsoft.Extensions.AI` adapters, shipped as separate packages.
- **Ecosystem connectors** — a `Microsoft.Extensions.VectorData` / Semantic Kernel vector-store connector.

## Requirements

- .NET 9 SDK
- A Redis deployment with the Redis Query Engine (`RediSearch`) module — [Redis 8](https://redis.io/docs/latest/operate/oss_and_stack/), [Redis Stack](https://redis.io/docs/latest/operate/oss_and_stack/install/install-stack/), or [Redis Cloud](https://redis.io/cloud/).
- `RedisJSON` for JSON-backed document workflows (included in Redis 8 / Redis Stack).
- Redis 8.4+ for native `FT.HYBRID` hybrid search.

You can start a local Redis with everything enabled via Docker:

```bash
docker run -d --name redis -p 6379:6379 redis:8
```

## Installation

Install the core package from NuGet:

```bash
dotnet add package RedisVL
```

Add extension packages only as you need them:

| Package | Purpose |
| --- | --- |
| `RedisVL` | Core library: schema, indexes, documents, queries, caches, routers |
| `RedisVL.Vectorizers.OpenAI` | OpenAI-backed embeddings |
| `RedisVL.Vectorizers.Cohere` | Cohere-backed embeddings |
| `RedisVL.Vectorizers.HuggingFace` | Hugging Face inference API embeddings |
| `RedisVL.Vectorizers.Onnx` | Local/offline ONNX SentenceTransformers embeddings |
| `RedisVL.Vectorizers.ExtensionsAI` | Adapter for any `Microsoft.Extensions.AI` `IEmbeddingGenerator` |
| `RedisVL.Rerankers.Cohere` | Cohere reranking |
| `RedisVL.Rerankers.Onnx` | Local/offline ONNX cross-encoder reranking |
| `RedisVL.Connectors.VectorData` | `Microsoft.Extensions.VectorData` / Semantic Kernel vector-store connector |

## Quick start

The core flow is: connect to Redis, define a schema, create the index, load documents, then query.

```csharp
using RedisVL.Filters;
using RedisVL.Indexes;
using RedisVL.Queries;
using RedisVL.Schema;
using StackExchange.Redis;

// 1. Connect to Redis (StackExchange.Redis).
using var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
var database = redis.GetDatabase();

// 2. Define a schema: an index over the "movies:" keyspace with a 2-dim vector field.
var schema = new SearchSchema(
    new IndexDefinition("movies-idx", "movies:", StorageType.Hash),
    [
        new TagFieldDefinition("genre"),
        new TextFieldDefinition("title"),
        new VectorFieldDefinition(
            "embedding",
            new VectorFieldAttributes(
                VectorAlgorithm.Hnsw,
                VectorDataType.Float32,
                VectorDistanceMetric.Cosine,
                dimensions: 2,
                m: 16,
                efConstruction: 200))
    ]);

// 3. Create the index (dropping any existing one with the same name).
var index = new SearchIndex(database, schema);
await index.CreateAsync(new CreateIndexOptions(overwrite: true, dropExistingDocuments: true));

// 4. Load documents. Here we write HASHes directly; in real apps embeddings come from a vectorizer.
static byte[] EncodeFloat32(float[] vector)
{
    var bytes = new byte[vector.Length * sizeof(float)];
    Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
    return bytes;
}

await database.HashSetAsync("movies:heat", new HashEntry[]
{
    new("title", "Heat"),
    new("genre", "crime"),
    new("embedding", EncodeFloat32(new[] { 1f, 0f }))
});

// 5. Run a nearest-neighbor query, filtered to the "crime" genre.
var query = VectorQuery.FromFloat32(
    fieldName: "embedding",
    vector: new[] { 1f, 0f },
    topK: 3,
    filter: Filter.Tag("genre").Eq("crime"),
    returnFields: ["title"],
    scoreAlias: "distance");

var results = await index.SearchAsync(query);
foreach (var doc in results.Documents)
{
    Console.WriteLine($"{doc.Values["title"]} (distance={doc.Values["distance"]})");
}
```

> Redis expects vector fields as raw little-endian float32 bytes, hence the `EncodeFloat32` helper. See [VectorSearchExample](examples/VectorSearchExample/README.md) for a complete, runnable version.

### JSON documents

Prefer JSON storage when you want to work with strongly-typed POCOs and JSONPath partial updates:

```csharp
var schema = SearchSchema.FromYamlFile("schema.yaml");     // or build in code
var index = new SearchIndex(database, schema);
await index.CreateAsync(new CreateIndexOptions(overwrite: true));

var movies = new[]
{
    new Movie("movie-1", "Heat", 1995, "crime"),
    new Movie("movie-2", "Arrival", 2016, "science-fiction")
};
await index.LoadJsonAsync(movies);

var arrival = await index.FetchJsonByIdAsync<Movie>("movie-2");

// Filter + text queries, projected onto a type
var query = new FilterQuery(
    Filter.And(
        Filter.Tag("genre").Eq("science-fiction"),
        Filter.Numeric("year").GreaterThan(1980)),
    returnFields: ["title", "year", "genre"]);
var scienceFiction = await index.SearchAsync<Movie>(query);
```

### Generating embeddings with a vectorizer

Vectorizer packages implement `IBatchTextVectorizer`, so you can turn text into embeddings and feed them to any query or cache:

```csharp
using RedisVL.Vectorizers.OpenAI;

var vectorizer = new OpenAiTextVectorizer(
    model: "text-embedding-3-small",
    apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
    new OpenAiVectorizerOptions { Dimensions = 256 });

float[] embedding = await vectorizer.VectorizeAsync("A linguist decodes an alien language.");
IReadOnlyList<float[]> batch = await vectorizer.VectorizeAsync(["first prompt", "second prompt"]);
```

### Semantic cache

`SemanticCache` stores prompt/response pairs keyed by embedding similarity, so repeated (or paraphrased) prompts return a cached answer instead of hitting your LLM:

```csharp
using RedisVL.Caches;
using RedisVL.Schema;

var cache = new SemanticCache(
    database,
    new SemanticCacheOptions(
        "semantic-cache-example",
        new VectorFieldAttributes(
            VectorAlgorithm.Flat, VectorDataType.Float32, VectorDistanceMetric.Cosine, dimensions: 3),
        distanceThreshold: 0.2d,
        timeToLive: TimeSpan.FromMinutes(10)));
await cache.CreateAsync();

await cache.StoreAsync(
    prompt: "How do I reset my password?",
    response: "Open Settings > Security > Reset password and follow the email link.",
    embedding: new[] { 1f, 0f, 0f });

// A near-identical embedding returns the cached response.
var hit = await cache.CheckAsync(prompt: "How do I reset my password?", embedding: new[] { 1f, 0f, 0f });
Console.WriteLine(hit?.Response);
```

## Connecting to cluster or Sentinel

`RedisVL` operates on a StackExchange.Redis `IDatabase`, so any connection you can build works. For cluster and Sentinel topologies, the `RedisConnectionFactory` helpers normalize seed-node lists and build `ConfigurationOptions` for you:

- `ConnectClusterAsync(...)` — cluster discovery
- `ConnectSentinelPrimaryAsync(...)` — Sentinel primary discovery
- `CreateClusterOptions(...)` / `CreateSentinelOptions(...)` — parsed option builders

## Examples

The [examples/](examples/README.md) directory contains runnable projects for every feature area. Run any of them from the repository root, for example:

```bash
dotnet run --project examples/VectorSearchExample/VectorSearchExample.csproj
```

Highlights:

| Area | Example |
| --- | --- |
| JSON schema, load, fetch, filter/text/aggregate queries | [JsonStorageExample](examples/JsonStorageExample/README.md) |
| Vector KNN, multi-vector, runtime tuning | [VectorSearchExample](examples/VectorSearchExample/README.md) |
| SVS-VAMANA index + vector compression | [SvsVamanaExample](examples/SvsVamanaExample/README.md) |
| Native `FT.HYBRID` hybrid search | [HybridSearchExample](examples/HybridSearchExample/README.md) |
| Semantic cache | [SemanticCacheExample](examples/SemanticCacheExample/README.md) |
| Semantic router | [SemanticRouterExample](examples/SemanticRouterExample/README.md) |
| Embeddings cache | [EmbeddingsCacheExample](examples/EmbeddingsCacheExample/README.md) |
| Semantic message history | [MessageHistoryExample](examples/MessageHistoryExample/README.md) |
| OpenAI / Cohere / Hugging Face / ONNX vectorizers | [OpenAiVectorizerExample](examples/OpenAiVectorizerExample/README.md), [CohereVectorizerExample](examples/CohereVectorizerExample/README.md), [HuggingFaceVectorizerExample](examples/HuggingFaceVectorizerExample/README.md), [OnnxVectorizerExample](examples/OnnxVectorizerExample/README.md) |
| Cohere / ONNX rerankers | [CohereRerankerExample](examples/CohereRerankerExample/README.md), [OnnxRerankerExample](examples/OnnxRerankerExample/README.md) |
| Microsoft.Extensions.VectorData / Semantic Kernel connector | [VectorDataConnectorExample](examples/VectorDataConnectorExample/README.md), [SemanticKernelConnectorExample](examples/SemanticKernelConnectorExample/README.md) |

## Documentation

Full documentation lives in the Antora docs site source:

- [Overview](docs-site/modules/ROOT/pages/index.adoc)
- [Getting Started](docs-site/modules/ROOT/pages/getting-started/index.adoc) — installation, connection topologies, environment variables
- [Core Features](docs-site/modules/ROOT/pages/core-features/index.adoc) — schema, indexes, documents, queries
- [Extensions](docs-site/modules/ROOT/pages/extensions/index.adoc) — vectorizers, rerankers, connectors
- [Testing](docs-site/modules/ROOT/pages/testing/index.adoc)

Build the docs locally from the repository root:

```bash
npm install
npm run docs:validate
```

## Contributing

Issues and pull requests are welcome. The solution file is `redis-vl-dotnet.sln`; build and test with the standard `dotnet` CLI:

```bash
dotnet build redis-vl-dotnet.sln
dotnet test
```

Integration tests need a reachable Redis with the Query Engine enabled. See [Testing](docs-site/modules/ROOT/pages/testing/index.adoc) for details.
