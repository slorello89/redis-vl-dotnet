# Testing

`redis-vl-dotnet` uses two test modes:

- Unit tests: run everywhere and do not require Redis.
- Integration tests: require Redis Stack or another Redis deployment with RediSearch and RedisJSON support.

## Unit Tests

Run the full solution test suite without setting any environment variables:

```bash
dotnet test redis-vl-dotnet.sln --no-restore
```

The integration tests are guarded by `RedisSearchIntegrationFact` and are skipped unless `REDIS_VL_REDIS_URL` is set.

Provider smoke tests are also guarded by provider-specific environment variables:

- `OPENAI_API_KEY` for OpenAI vectorizer smoke tests
- `HF_TOKEN` for Hugging Face vectorizer smoke tests
- `COHERE_API_KEY` for Cohere reranker smoke tests

## Integration Tests

Start Redis Stack locally:

```bash
docker compose -f docker-compose.integration.yml up -d redis-stack
```

Point the tests at that instance:

```bash
export REDIS_VL_REDIS_URL=localhost:6379
```

Run the test project:

```bash
dotnet test tests/RedisVlDotNet.Tests/RedisVlDotNet.Tests.csproj --no-restore
```

Stop the local Redis Stack container when you are done:

```bash
docker compose -f docker-compose.integration.yml down
```

## Deterministic Redis Harness

The Redis-backed tests keep query assertions reproducible by:

- Using shared seed datasets under `tests/RedisVlDotNet.Tests/Indexes/SearchIndexSeedData.cs` for filter, vector, and hybrid scenarios.
- Creating unique index names and key prefixes per test so parallel or repeated runs do not reuse stale Redis data.
- Polling for index readiness through `RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(...)` or `WaitForAsync(...)` instead of fixed sleeps.

## CI

GitHub Actions runs:

1. `dotnet build redis-vl-dotnet.sln --no-restore`
2. `dotnet test redis-vl-dotnet.sln --no-build --verbosity normal`

The workflow provisions `redis/redis-stack-server` as a service container and sets `REDIS_VL_REDIS_URL=localhost:6379`, so the same integration tests that are opt-in locally run automatically in CI.
