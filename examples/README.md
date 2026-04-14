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

| Feature area | Antora entry point | Example or command | Runtime requirements |
| --- | --- | --- | --- |
| Advanced schema options, YAML loading, from-existing index, index listing, JSON partial updates, `TextQuery`, aggregation, clear helper | [Core Features](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/core-features/index.adoc) | [JsonStorageExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/JsonStorageExample/README.md) | RediSearch + RedisJSON. Supports `REDIS_VL_REDIS_URL`, `REDIS_VL_REDIS_CLUSTER_NODES`, or `REDIS_VL_REDIS_SENTINEL_NODES` plus `REDIS_VL_REDIS_SENTINEL_SERVICE_NAME` |
| Vector query basics, runtime vector search tuning, `MultiVectorQuery`, aggregate hybrid search, HASH partial updates | [Core Features](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/core-features/index.adoc) | [VectorSearchExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/VectorSearchExample/README.md) | RediSearch with vector similarity support and optional `REDIS_VL_REDIS_URL` |
| Exact-input embedding reuse with TTL | [EmbeddingsCache](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/core-features/embeddings-cache.adoc) | [EmbeddingsCacheExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/EmbeddingsCacheExample/README.md) | Basic Redis only with optional `REDIS_VL_REDIS_URL` |
| Semantic message history with recency and semantic retrieval | [SemanticMessageHistory](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/core-features/semantic-message-history.adoc) | [MessageHistoryExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/MessageHistoryExample/README.md) | RediSearch with vector similarity support and optional `REDIS_VL_REDIS_URL`; no provider credentials required |
| Semantic cache filter fields and metadata payloads | [SemanticCache](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/core-features/semantic-cache.adoc) | [SemanticCacheExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/SemanticCacheExample/README.md) | RediSearch with vector similarity support and optional `REDIS_VL_REDIS_URL` |
| Semantic route registration and nearest-route matching | [SemanticRouter](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/core-features/semantic-router.adoc) | [SemanticRouterExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/SemanticRouterExample/README.md) | RediSearch with vector similarity support and optional `REDIS_VL_REDIS_URL`; no provider credentials required |
| OpenAI vectorizer package | [OpenAI Vectorizer](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/extensions/openai-vectorizer.adoc) | [OpenAiVectorizerExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/OpenAiVectorizerExample/README.md) | RediSearch with vector similarity support, `OPENAI_API_KEY`, and optional `OPENAI_EMBEDDING_MODEL`, `OPENAI_EMBEDDING_DIMENSIONS`, `REDIS_VL_REDIS_URL` |
| Hugging Face vectorizer package | [Hugging Face Vectorizer](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/extensions/huggingface-vectorizer.adoc) | [HuggingFaceVectorizerExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/HuggingFaceVectorizerExample/README.md) | RediSearch with vector similarity support, `HF_TOKEN`, and optional `HF_EMBEDDING_MODEL`, `REDIS_VL_REDIS_URL` |
| Cohere reranker package | [Cohere Reranker](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/extensions/cohere-reranker.adoc) | [CohereRerankerExample](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/CohereRerankerExample/README.md) | RediSearch + RedisJSON, `COHERE_API_KEY`, and optional `COHERE_RERANK_MODEL`, `REDIS_VL_REDIS_URL` |
| CLI index and schema commands | [CLI](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/cli/index.adoc) | `dotnet run --project src/RedisVlDotNet.Cli -- ...` | Index commands require RediSearch plus `--redis` or `REDIS_VL_REDIS_URL`; JSON-backed index creation also requires RedisJSON |

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

- [Getting Started](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/getting-started/index.adoc)
- [Examples](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/examples/index.adoc)
- [Testing](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/testing/index.adoc)
- [CLI](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/cli/index.adoc)
