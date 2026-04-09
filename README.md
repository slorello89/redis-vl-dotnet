# redis-vl-dotnet

`redis-vl-dotnet` is a .NET-native Redis Vector Library for Redis Search and vector workloads.

The implementation plan for v1 is defined in [docs/v1-architecture.md](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/v1-architecture.md). That document is the contract for feature parity, scope, and public API direction across future Ralph iterations.

Start with [docs/getting-started.md](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/getting-started.md) for the core install, connection, schema, index, document, and query flow.
Run [examples/JsonStorageExample/README.md](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/JsonStorageExample/README.md) for a checked-in console app that exercises the JSON storage workflow end to end.

## Current Workspace

- `docs/v1-architecture.md`: v1 parity matrix and architecture decisions
- `docs/getting-started.md`: end-to-end guide for creating an index, loading documents, and running basic queries
- `docs/testing.md`: local and CI test harness instructions for unit and Redis-backed integration coverage
- `examples/JsonStorageExample`: runnable console app for JSON-backed schema, load, fetch, and query flows
- `redis-vl-dotnet.sln`: minimal solution scaffold so `dotnet build` is a valid repository quality gate from the first iteration
- `prd.json`: Ralph execution plan
- `progress.txt`: iteration log and reusable codebase patterns

## Testing

Run unit tests without Redis:

```bash
dotnet test redis-vl-dotnet.sln --no-restore
```

Start a local Redis Stack instance for integration tests:

```bash
docker compose -f docker-compose.integration.yml up -d redis-stack
export REDIS_VL_REDIS_URL=localhost:6379
```

Then run the Redis-backed integration suite:

```bash
dotnet test tests/RedisVlDotNet.Tests/RedisVlDotNet.Tests.csproj --no-restore
```

The full local and CI workflow is documented in [docs/testing.md](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/testing.md).
