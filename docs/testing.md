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
Cluster-specific integration coverage is guarded by `REDIS_VL_REDIS_CLUSTER_NODES` and is skipped unless a RediSearch-capable cluster is available.
Sentinel-specific integration coverage is guarded by `REDIS_VL_REDIS_SENTINEL_NODES` and `REDIS_VL_REDIS_SENTINEL_SERVICE_NAME` and is skipped unless a RediSearch-capable Sentinel deployment is available.

Provider smoke tests are also guarded by provider-specific environment variables:

- `OPENAI_API_KEY` for OpenAI vectorizer smoke tests
- `HF_TOKEN` for Hugging Face vectorizer smoke tests
- `COHERE_API_KEY` for Cohere reranker smoke tests

## Test Matrix

Use these tiers to validate the parity surface locally and in CI:

| Tier | Scope | Command | Environment |
| --- | --- | --- | --- |
| Solution build | All library, CLI, and test projects compile | `dotnet build redis-vl-dotnet.sln --no-restore` | None |
| Default test suite | Unit tests plus any Redis/provider tests whose env vars are present | `dotnet test redis-vl-dotnet.sln --no-build --verbosity normal` | None required; gated tests skip themselves |
| Redis integration suite | Search index, schema, workflow, cache, and CLI integration coverage against one Redis deployment | `dotnet test tests/RedisVlDotNet.Tests/RedisVlDotNet.Tests.csproj --no-restore` | `REDIS_VL_REDIS_URL` |
| CLI-focused subset | CLI parser plus Redis-backed CLI lifecycle coverage | `dotnet test tests/RedisVlDotNet.Tests/RedisVlDotNet.Tests.csproj --no-restore --filter FullyQualifiedName~RedisVlCli` | `REDIS_VL_REDIS_URL` for integration cases |
| Cluster topology subset | Cluster connection helpers | `dotnet test tests/RedisVlDotNet.Tests/RedisVlDotNet.Tests.csproj --no-restore --filter FullyQualifiedName~RedisClusterConnectionIntegrationTests` | `REDIS_VL_REDIS_CLUSTER_NODES` and any auth or TLS vars |
| Sentinel topology subset | Sentinel discovery helpers | `dotnet test tests/RedisVlDotNet.Tests/RedisVlDotNet.Tests.csproj --no-restore --filter FullyQualifiedName~RedisSentinelConnectionIntegrationTests` | `REDIS_VL_REDIS_SENTINEL_NODES`, `REDIS_VL_REDIS_SENTINEL_SERVICE_NAME`, and any auth or TLS vars |
| OpenAI smoke | Provider package request and live embedding flow | `dotnet test tests/RedisVlDotNet.Tests/RedisVlDotNet.Tests.csproj --no-restore --filter FullyQualifiedName~OpenAi` | `OPENAI_API_KEY` |
| Hugging Face smoke | Provider package request and live embedding flow | `dotnet test tests/RedisVlDotNet.Tests/RedisVlDotNet.Tests.csproj --no-restore --filter FullyQualifiedName~HuggingFace` | `HF_TOKEN` |
| Cohere smoke | Provider package request and live rerank flow | `dotnet test tests/RedisVlDotNet.Tests/RedisVlDotNet.Tests.csproj --no-restore --filter FullyQualifiedName~Cohere` | `COHERE_API_KEY` |
| Example build sweep | Confirms all runnable sample projects still compile | `find examples -name '*.csproj' -print0 | xargs -0 -n1 dotnet build --no-restore` | Provider env vars not required for build-only validation |

Provider package prerequisites for the runnable examples match the smoke-test gates:

- `examples/OpenAiVectorizerExample` and `OpenAiTextVectorizerSmokeTests`: `OPENAI_API_KEY`
- `examples/HuggingFaceVectorizerExample` and `HuggingFaceTextVectorizerSmokeTests`: `HF_TOKEN`
- `examples/CohereRerankerExample` and `CohereTextRerankerSmokeTests`: `COHERE_API_KEY`

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

To exercise the CLI-specific coverage only:

```bash
dotnet test tests/RedisVlDotNet.Tests/RedisVlDotNet.Tests.csproj --no-restore --filter FullyQualifiedName~RedisVlCli
```

To run the gated cluster integration test, point the suite at a Redis cluster instead:

```bash
export REDIS_VL_REDIS_CLUSTER_NODES=127.0.0.1:7000,127.0.0.1:7001,127.0.0.1:7002
export REDIS_VL_REDIS_USER=default
export REDIS_VL_REDIS_PASSWORD=secret
export REDIS_VL_REDIS_SSL=false
dotnet test tests/RedisVlDotNet.Tests/RedisVlDotNet.Tests.csproj --no-restore --filter FullyQualifiedName~RedisClusterConnectionIntegrationTests
```

To run the gated Sentinel integration test, point the suite at a Redis Sentinel deployment instead:

```bash
export REDIS_VL_REDIS_SENTINEL_NODES=127.0.0.1:26379,127.0.0.1:26380,127.0.0.1:26381
export REDIS_VL_REDIS_SENTINEL_SERVICE_NAME=mymaster
export REDIS_VL_REDIS_USER=default
export REDIS_VL_REDIS_PASSWORD=secret
export REDIS_VL_REDIS_SSL=false
dotnet test tests/RedisVlDotNet.Tests/RedisVlDotNet.Tests.csproj --no-restore --filter FullyQualifiedName~RedisSentinelConnectionIntegrationTests
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
- Running CLI integration coverage in-process through `RedisVlCliApplication` so parser behavior and Redis operations share the same environment-gated test harness.

## CI

GitHub Actions runs:

1. `dotnet build redis-vl-dotnet.sln --no-restore`
2. `dotnet test redis-vl-dotnet.sln --no-build --verbosity normal`
3. Example builds should remain green locally before merging parity-surface doc or sample updates, even though they are not yet part of the default CI workflow.

The workflow provisions `redis/redis-stack-server` as a service container and sets `REDIS_VL_REDIS_URL=localhost:6379`, so the same integration tests that are opt-in locally run automatically in CI.
