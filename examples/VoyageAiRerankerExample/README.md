# Voyage AI Reranker Example

This example is a runnable .NET 9 console app that demonstrates text search plus Voyage AI reranking through `RedisVL.Rerankers.VoyageAI`:

- create a JSON-backed search index with support articles
- retrieve an initial candidate set from Redis with `TextQuery`
- rerank those candidates through the Voyage AI extension package
- print the original Redis order alongside the Voyage AI-adjusted order
- drop the example index and documents

## Prerequisites

- .NET 9 SDK
- Redis Stack or another Redis deployment with RediSearch and RedisJSON enabled

Additional prerequisites:

- `VOYAGE_API_KEY`
- `VOYAGE_RERANK_MODEL` (optional model override; defaults to `rerank-2.5`)

Start Redis locally from the repository root if needed:

```bash
docker compose -f docker-compose.integration.yml up -d redis-stack
export REDIS_VL_REDIS_URL=localhost:6379
```

## Run

From the repository root:

```bash
dotnet run --project examples/VoyageAiRerankerExample/VoyageAiRerankerExample.csproj
```

The example uses `REDIS_VL_REDIS_URL` when it is set, and otherwise falls back to `localhost:6379`. It fails fast with an explicit message when `VOYAGE_API_KEY` is missing.

## Related Docs

- [Voyage AI Reranker](../../docs-site/modules/ROOT/pages/extensions/voyageai-reranker.adoc)
- [Reranker Abstractions](../../docs-site/modules/ROOT/pages/extensions/reranker-abstractions.adoc)
- [Examples index](../README.md)
