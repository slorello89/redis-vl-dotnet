# JSON Storage Example

This example is a runnable .NET 9 console app that demonstrates a JSON-backed `redis-vl-dotnet` flow with an advanced schema loaded from `schema.yaml` on disk:

- load a schema from YAML
- create a JSON index
- reconnect to the existing index from Redis metadata
- list existing search indexes
- load sample documents
- fetch one document by id
- partially update JSON fields by id and by key
- run filter, text, aggregation, and count queries
- clear indexed documents while keeping the index
- drop the example index

## Prerequisites

- .NET 9 SDK
- Redis Stack or another Redis deployment with RediSearch and RedisJSON enabled

Start Redis locally from the repository root if needed:

```bash
docker compose -f docker-compose.integration.yml up -d redis-stack
export REDIS_VL_REDIS_URL=localhost:6379
```

For a Redis cluster deployment, set seed nodes instead of `REDIS_VL_REDIS_URL`:

```bash
export REDIS_VL_REDIS_CLUSTER_NODES=127.0.0.1:7000,127.0.0.1:7001,127.0.0.1:7002
export REDIS_VL_REDIS_USER=default
export REDIS_VL_REDIS_PASSWORD=secret
export REDIS_VL_REDIS_SSL=false
```

For a Redis Sentinel deployment, set the Sentinel nodes and service name instead:

```bash
export REDIS_VL_REDIS_SENTINEL_NODES=127.0.0.1:26379,127.0.0.1:26380,127.0.0.1:26381
export REDIS_VL_REDIS_SENTINEL_SERVICE_NAME=mymaster
export REDIS_VL_REDIS_USER=default
export REDIS_VL_REDIS_PASSWORD=secret
export REDIS_VL_REDIS_SSL=false
```

## Run

From the repository root:

```bash
dotnet run --project examples/JsonStorageExample/JsonStorageExample.csproj
```

The example uses `REDIS_VL_REDIS_URL` when it is set, and otherwise falls back to `localhost:6379`.
When `REDIS_VL_REDIS_SENTINEL_NODES` and `REDIS_VL_REDIS_SENTINEL_SERVICE_NAME` are set, the example uses `RedisConnectionFactory.ConnectSentinelPrimaryAsync(...)` and treats Sentinel discovery as the primary connection path.
When `REDIS_VL_REDIS_CLUSTER_NODES` is set without Sentinel settings, the example uses `RedisConnectionFactory.ConnectClusterAsync(...)` and treats those seed nodes as the primary connection path.

## Related Docs

- [Examples index](../README.md)
- [Core Features](../../docs-site/modules/ROOT/pages/core-features/index.adoc)
- [Getting Started](../../docs-site/modules/ROOT/pages/getting-started/index.adoc)
- [Testing](../../docs-site/modules/ROOT/pages/testing/index.adoc)
