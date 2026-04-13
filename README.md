# redis-vl-dotnet

`redis-vl-dotnet` is a .NET-native Redis Vector Library for Redis Search and vector workloads.

The active implementation roadmap is defined in [docs/parity-roadmap.md](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/parity-roadmap.md). That document is the current contract for cross-language parity scope, intentional .NET-native differences, and feature priority across future Ralph iterations.

Start with [docs/getting-started.md](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/getting-started.md) for the core install, connection, schema, index, document, and query flow.
Browse [examples/README.md](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/README.md) for the runnable sample index, Redis prerequisites, and links to each example's local run instructions.
Read [docs/extensions.md](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/extensions.md) for the vectorizer abstractions package split and provider-extension layout.
See [examples/OpenAiVectorizerExample/README.md](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/OpenAiVectorizerExample/README.md) for a provider-backed OpenAI embedding flow with `SemanticCache`.

## Current Workspace

- `docs/parity-roadmap.md`: current .NET/Java/Python parity matrix and roadmap decisions
- `docs/getting-started.md`: end-to-end guide for creating an index, loading documents, and running basic queries
- `docs/extensions.md`: extension package architecture and shared text vectorizer contracts
- `docs/testing.md`: local and CI test harness instructions for unit and Redis-backed integration coverage
- `examples/README.md`: examples index with prerequisites and workflow summaries
- `examples/JsonStorageExample`: runnable console app for JSON-backed schema, load, fetch, and query flows
- `examples/VectorSearchExample`: runnable console app for vector-field schema, deterministic seed data, and nearest-neighbor search
- `examples/OpenAiVectorizerExample`: runnable console app for OpenAI-backed vectorization with `SemanticCache`
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
