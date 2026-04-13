# Vector Search Example

This example is a runnable .NET 9 console app that demonstrates the smallest vector-search flow in `redis-vl-dotnet`:

- define a HASH-backed schema with two vector fields
- seed deterministic sample documents with raw float32 embedding bytes for plot and poster embeddings
- partially update top-level HASH fields with `UpdateHashByIdAsync(...)`
- execute a nearest-neighbor query against the vector field
- execute a weighted `MultiVectorQuery` across both vector fields
- execute an aggregate hybrid query that combines text predicates, KNN retrieval, and aggregation reducers
- inspect returned titles, summaries, single-vector distances, and weighted combined distances
- drop the example index and documents

HASH partial updates in this example use Redis `HSET` semantics:

- update targets are plain top-level field names such as `summary` and `genre`
- fields omitted from the update stay unchanged in the stored HASH
- `null` values are not supported by the helper because HASH writes in this library treat nulls as missing fields, not deletions

## Prerequisites

- .NET 9 SDK
- Redis Stack or another Redis deployment with RediSearch enabled

Start Redis locally from the repository root if needed:

```bash
docker compose -f docker-compose.integration.yml up -d redis-stack
export REDIS_VL_REDIS_URL=localhost:6379
```

## Run

From the repository root:

```bash
dotnet run --project examples/VectorSearchExample/VectorSearchExample.csproj
```

The example uses `REDIS_VL_REDIS_URL` when it is set, and otherwise falls back to `localhost:6379`.

## Related Docs

- [Examples index](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/README.md)
- [Getting started guide](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/getting-started.md)
- [Testing and local Redis setup](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/testing.md)
