# Vector Search Example

This example is a runnable .NET 9 console app that demonstrates the smallest vector-search flow in `redis-vl-dotnet`:

- define a HASH-backed schema with a vector field
- seed deterministic sample documents with raw float32 embedding bytes
- execute a nearest-neighbor query against the vector field
- inspect returned titles, summaries, and vector distances
- drop the example index and documents

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
