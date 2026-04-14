# Examples

Use this directory as the entry point for runnable `redis-vl-dotnet` samples.

## Prerequisites

All examples currently assume:

- .NET 9 SDK
- Redis Stack or another Redis deployment with RediSearch enabled
- `REDIS_VL_REDIS_URL` set when Redis is not reachable at `localhost:6379`
- `REDIS_VL_REDIS_CLUSTER_NODES` or `REDIS_VL_REDIS_SENTINEL_NODES` plus `REDIS_VL_REDIS_SENTINEL_SERVICE_NAME` when you want the JSON example to use cluster or Sentinel discovery instead of a direct endpoint

Examples that use JSON storage also require RedisJSON support.

Start Redis locally from the repository root if needed:

```bash
docker compose -f docker-compose.integration.yml up -d redis-stack
export REDIS_VL_REDIS_URL=localhost:6379
```

## Available Examples

## Feature Coverage

Use this map when you want the fastest path to a parity feature area:

| Feature area | Example or doc entry point | Notes |
| --- | --- | --- |
| Advanced schema options, YAML loading, from-existing index, index listing, JSON partial updates, `TextQuery`, aggregation, clear helper | [JsonStorageExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/JsonStorageExample/README.md) | Also covers cluster and Sentinel connection environment variables |
| Vector query basics, runtime vector search tuning, `MultiVectorQuery`, aggregate hybrid search, HASH partial updates | [VectorSearchExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/VectorSearchExample/README.md) | HASH-backed workflow with deterministic vector seed data |
| Exact-input embedding reuse with TTL | [EmbeddingsCacheExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/EmbeddingsCacheExample/README.md) | Uses Redis string storage and does not require RediSearch |
| Semantic message history with recency and semantic retrieval | [MessageHistoryExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/MessageHistoryExample/README.md) | Uses an in-process sample vectorizer so no provider credentials are required |
| Semantic cache filter fields and metadata payloads | [SemanticCacheExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/SemanticCacheExample/README.md) | Shows tenant/model filter composition |
| Semantic route registration and nearest-route matching | [SemanticRouterExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/SemanticRouterExample/README.md) | Uses an in-process sample vectorizer so no provider credentials are required |
| OpenAI vectorizer package | [OpenAiVectorizerExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/OpenAiVectorizerExample/README.md) | Requires `OPENAI_API_KEY` |
| Hugging Face vectorizer package | [HuggingFaceVectorizerExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/HuggingFaceVectorizerExample/README.md) | Requires `HF_TOKEN` |
| Cohere reranker package | [CohereRerankerExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/CohereRerankerExample/README.md) | Requires `COHERE_API_KEY` |
| CLI index and schema commands | [CLI docs](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/cli/index.adoc) | Covers build/install, command discovery, `index create/list/info/clear/delete`, and `schema validate/show` |

### [JsonStorageExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/JsonStorageExample/README.md)

Demonstrates the core JSON workflow:

- define a JSON-backed schema
- create an index
- load sample documents
- fetch a document by id
- run filter, text, and count queries
- clear indexed documents while preserving the index
- drop the example index

Redis prerequisites:

- RediSearch
- RedisJSON

Run it from the repository root:

```bash
dotnet run --project examples/JsonStorageExample/JsonStorageExample.csproj
```

### [VectorSearchExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/VectorSearchExample/README.md)

Demonstrates the core vector workflow:

- define a HASH-backed schema with a vector field
- seed deterministic float32 embeddings
- run a nearest-neighbor query
- run an aggregate hybrid query over the vector candidates
- inspect returned distances and grouped aggregates
- drop the example index and documents

Redis prerequisites:

- RediSearch with vector similarity support

Run it from the repository root:

```bash
dotnet run --project examples/VectorSearchExample/VectorSearchExample.csproj
```

### [EmbeddingsCacheExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/EmbeddingsCacheExample/README.md)

Demonstrates exact-input embedding reuse:

- create an `EmbeddingsCache` with a per-run namespace
- store an embedding for one input string
- look up the cached embedding by the same input
- overwrite the stored embedding and confirm the new value

Run it from the repository root:

```bash
dotnet run --project examples/EmbeddingsCacheExample/EmbeddingsCacheExample.csproj
```

### [MessageHistoryExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/MessageHistoryExample/README.md)

Demonstrates semantic message history retrieval:

- create a HASH-backed semantic message history index
- append session messages with embeddings and metadata
- retrieve the most recent messages for one session
- retrieve semantically relevant messages within the same session
- drop the example index and documents

Redis prerequisites:

- RediSearch with vector similarity support

Run it from the repository root:

```bash
dotnet run --project examples/MessageHistoryExample/MessageHistoryExample.csproj
```

### [SemanticCacheExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/SemanticCacheExample/README.md)

Demonstrates enriched semantic cache retrieval:

- create a HASH-backed semantic cache with filterable fields
- store semantic cache entries with metadata payloads
- keep tenant-specific prompt variants in the same cache
- retrieve a filtered semantic cache hit
- drop the example index and documents

Redis prerequisites:

- RediSearch with vector similarity support

Run it from the repository root:

```bash
dotnet run --project examples/SemanticCacheExample/SemanticCacheExample.csproj
```

### [SemanticRouterExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/SemanticRouterExample/README.md)

Demonstrates nearest-route matching:

- create a `SemanticRouter` with a local sample vectorizer
- add routes for multiple intent categories
- route a new utterance to the nearest stored route
- drop the example index and documents

Redis prerequisites:

- RediSearch with vector similarity support

Run it from the repository root:

```bash
dotnet run --project examples/SemanticRouterExample/SemanticRouterExample.csproj
```

### [OpenAiVectorizerExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/OpenAiVectorizerExample/README.md)

Demonstrates provider-backed vectorization with `SemanticCache`:

- create a HASH-backed semantic cache sized for the configured OpenAI embedding dimensions
- generate seed embeddings through the OpenAI extension package in one batch request
- store a semantic cache entry with the generated embedding
- retrieve a semantically similar cache hit by vectorizing a new prompt through OpenAI
- drop the example index and documents

Redis prerequisites:

- RediSearch with vector similarity support

Additional prerequisites:

- `OPENAI_API_KEY`

Run it from the repository root:

```bash
dotnet run --project examples/OpenAiVectorizerExample/OpenAiVectorizerExample.csproj
```

### [HuggingFaceVectorizerExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/HuggingFaceVectorizerExample/README.md)

Demonstrates provider-backed vectorization with `SemanticCache`:

- create a HASH-backed semantic cache sized from a live Hugging Face embedding response
- generate seed embeddings through the Hugging Face extension package in one batch request
- store a semantic cache entry with the generated embedding
- retrieve a semantically similar cache hit by vectorizing a new prompt through Hugging Face
- drop the example index and documents

Redis prerequisites:

- RediSearch with vector similarity support

Additional prerequisites:

- `HF_TOKEN`

Run it from the repository root:

```bash
dotnet run --project examples/HuggingFaceVectorizerExample/HuggingFaceVectorizerExample.csproj
```

### [CohereRerankerExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/CohereRerankerExample/README.md)

Demonstrates text search plus Cohere reranking:

- create a JSON-backed search index with support articles
- retrieve an initial candidate set from Redis with `TextQuery`
- rerank those candidates through the Cohere extension package
- print the original Redis order alongside the Cohere-adjusted order
- drop the example index and documents

Redis prerequisites:

- RediSearch
- RedisJSON

Additional prerequisites:

- `COHERE_API_KEY`

Run it from the repository root:

```bash
dotnet run --project examples/CohereRerankerExample/CohereRerankerExample.csproj
```

## Related Docs

- [Getting started guide](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/getting-started.md)
- [Testing and local Redis setup](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/testing.md)
- [Current parity roadmap and matrix](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/parity-roadmap.md)
