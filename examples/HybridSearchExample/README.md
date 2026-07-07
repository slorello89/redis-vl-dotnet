# Hybrid Search Example

This example is a runnable .NET 9 console app that demonstrates the native `FT.HYBRID` hybrid search flow in `redis-vl-dotnet` via `HybridSearchQuery`:

- define a HASH-backed schema with a text branch (`title`, `summary`), a `genre` tag, and an HNSW vector field
- seed deterministic sample documents with raw float32 embedding bytes
- run a `HybridSearchQuery` with **linear fusion** (`COMBINE LINEAR`, explicit `alpha`/`beta`) and `EF_RUNTIME` tuning
- run the same query with the **server-default fusion** (RRF, window 20, constant 60) by omitting the combination
- run **reciprocal rank fusion** with an explicit `constant` and `window` (`COMBINE RRF`)
- apply a **vector pre-filter** (`VSIM ... FILTER`) that restricts the candidate set before fusion
- read the source document key (result `Id`) and the fused score (`HybridSearchQuery.ScoreField` / `__score`)
- project results onto a typed record with `SearchAsync<T>(...)`
- drop the example index and documents

Unlike `HybridQuery` (a single `FT.SEARCH` expression) and `AggregateHybridQuery` (`FT.AGGREGATE`), `HybridSearchQuery` issues a real `FT.HYBRID` command: the text branch (`SEARCH`) and the vector branch (`VSIM`) are scored independently and fused server-side.

## Prerequisites

- .NET 9 SDK
- **Redis 8.4 or newer** with RediSearch enabled (`FT.HYBRID` is unavailable on older servers — use `HybridQuery` or `AggregateHybridQuery` instead)

Start Redis locally from the repository root if needed:

```bash
docker compose -f docker-compose.integration.yml up -d redis
export REDIS_VL_REDIS_URL=localhost:6379
```

## Run

From the repository root:

```bash
dotnet run --project examples/HybridSearchExample/HybridSearchExample.csproj
```

The example uses `REDIS_VL_REDIS_URL` when it is set, and otherwise falls back to `localhost:6379`.

## Related Docs

- [HybridSearchQuery](../../docs-site/modules/ROOT/pages/core-features/hybrid-search-query.adoc)
- [Examples index](../README.md)
- [Core Features](../../docs-site/modules/ROOT/pages/core-features/index.adoc)
- [Getting Started](../../docs-site/modules/ROOT/pages/getting-started/index.adoc)
- [Testing](../../docs-site/modules/ROOT/pages/testing/index.adoc)
