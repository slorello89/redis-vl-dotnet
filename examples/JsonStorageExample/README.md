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

## Run

From the repository root:

```bash
dotnet run --project examples/JsonStorageExample/JsonStorageExample.csproj
```

The example uses `REDIS_VL_REDIS_URL` when it is set, and otherwise falls back to `localhost:6379`.
When `REDIS_VL_REDIS_CLUSTER_NODES` is set, the example uses `RedisConnectionFactory.ConnectClusterAsync(...)` and treats those seed nodes as the primary connection path.

## Related Docs

- [Examples index](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/README.md)
- [Getting started guide](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/getting-started.md)
- [Testing and local Redis setup](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/testing.md)
