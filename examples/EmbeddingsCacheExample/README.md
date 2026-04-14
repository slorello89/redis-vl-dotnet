# Embeddings Cache Example

Demonstrates exact-input embedding reuse:

- create an `EmbeddingsCache` with a per-run namespace and TTL
- store an embedding with Python-style `SetAsync(...)`
- look up the cached entry with `GetAsync(...)`
- inspect the Redis key returned from the write call
- overwrite the cached embedding and confirm the new value is returned

## Prerequisites

- .NET 9 SDK
- Redis reachable at `localhost:6379`, or `REDIS_VL_REDIS_URL` set to another endpoint

Start Redis locally from the repository root if needed:

```bash
docker compose -f docker-compose.integration.yml up -d redis-stack
export REDIS_VL_REDIS_URL=localhost:6379
```

This example uses plain Redis string operations, so it does not require RediSearch or provider credentials.

## Run

Run it from the repository root:

```bash
dotnet run --project examples/EmbeddingsCacheExample/EmbeddingsCacheExample.csproj
```

## Related Docs

- [Examples index](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/README.md)
- [EmbeddingsCache](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/core-features/embeddings-cache.adoc)
- [Getting Started](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/getting-started/index.adoc)
