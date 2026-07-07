# SVS-VAMANA Example

This example is a runnable .NET 9 console app that demonstrates the SVS-VAMANA vector index with vector compression in `redis-vl-dotnet`:

- define a HASH-backed schema with an `SvsVamana` vector field
- enable LVQ8 vector compression and set build-time graph knobs (`graphMaxDegree`, `constructionWindowSize`, `searchWindowSize`)
- seed deterministic sample documents with raw float32 embedding bytes
- run a nearest-neighbor query with SVS-VAMANA query-time runtime knobs (`searchWindowSize`, `useSearchHistory`, `searchBufferCapacity`)
- run a vector range query with an `epsilon` runtime knob
- inspect returned titles and distances
- drop the example index and documents

SVS-VAMANA is a graph-based vector index that supports vector compression (LVQ and LeanVec) to reduce memory usage. `VectorCompression` values are `None`, `Lvq8`, `Lvq4`, `Lvq4x4`, `Lvq4x8`, `LeanVec4x8`, and `LeanVec8x8`.

## Prerequisites

- .NET 9 SDK
- Redis 8.x (or newer) with RediSearch vector support — SVS-VAMANA is not available on older releases

Start Redis locally from the repository root if needed:

```bash
docker compose -f docker-compose.integration.yml up -d redis
export REDIS_VL_REDIS_URL=localhost:6379
```

## Run

From the repository root:

```bash
dotnet run --project examples/SvsVamanaExample/SvsVamanaExample.csproj
```

The example uses `REDIS_VL_REDIS_URL` when it is set, and otherwise falls back to `localhost:6379`.

## Related Docs

- [Examples index](../README.md)
- [Field Definitions](../../docs-site/modules/ROOT/pages/core-features/field-definitions.adoc)
- [Vector Query](../../docs-site/modules/ROOT/pages/core-features/vector-query.adoc)
- [Core Features](../../docs-site/modules/ROOT/pages/core-features/index.adoc)
