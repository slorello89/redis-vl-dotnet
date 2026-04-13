# Examples

Use this directory as the entry point for runnable `redis-vl-dotnet` samples.

## Prerequisites

All examples currently assume:

- .NET 9 SDK
- Redis Stack or another Redis deployment with RediSearch enabled
- `REDIS_VL_REDIS_URL` set when Redis is not reachable at `localhost:6379`

Examples that use JSON storage also require RedisJSON support.

Start Redis locally from the repository root if needed:

```bash
docker compose -f docker-compose.integration.yml up -d redis-stack
export REDIS_VL_REDIS_URL=localhost:6379
```

## Available Examples

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

## Related Docs

- [Getting started guide](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/getting-started.md)
- [Testing and local Redis setup](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/testing.md)
- [v1 architecture and parity matrix](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/v1-architecture.md)
