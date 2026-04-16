# Semantic Cache Example

Demonstrates an enriched semantic cache workflow:

- create a HASH-backed semantic cache with explicit filterable fields
- store semantic cache entries with JSON metadata payloads
- keep multiple prompt variants separated by tenant and model filters
- retrieve a semantic cache hit with a composed RedisVL filter
- drop the example index and documents

## Prerequisites

- .NET 9 SDK
- Redis Stack or another Redis deployment with RediSearch enabled
- Optional: `REDIS_VL_REDIS_URL` to point at a Redis instance other than `localhost:6379`

Redis prerequisites:

- RediSearch with vector similarity support

Start Redis locally from the repository root if needed:

```bash
docker compose -f docker-compose.integration.yml up -d redis-stack
export REDIS_VL_REDIS_URL=localhost:6379
```

## Run

Run it from the repository root:

```bash
dotnet run --project examples/SemanticCacheExample/SemanticCacheExample.csproj
```

## Related Docs

- [Examples index](../README.md)
- [SemanticCache](../../docs-site/modules/ROOT/pages/core-features/semantic-cache.adoc)
- [Testing](../../docs-site/modules/ROOT/pages/testing/index.adoc)
