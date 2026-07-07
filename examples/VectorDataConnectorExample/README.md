# Vector Data Connector Example

This example is a runnable .NET 9 console app that demonstrates the `RedisVL.Connectors.VectorData`
package — a Microsoft.Extensions.VectorData (MEVD) connector backed by RedisVL. The same
`VectorStore` / `VectorStoreCollection<TKey, TRecord>` types are consumable by Semantic Kernel and
the broader Microsoft.Extensions.AI ecosystem.

It shows:

- create a `RedisVLVectorStore` over a StackExchange.Redis `IDatabase`
- map a POCO with `[VectorStoreKey]`, `[VectorStoreData]`, and `[VectorStoreVector]` attributes
- create the backing JSON index with `EnsureCollectionExistsAsync()`
- embed movie summaries with OpenAI through a Microsoft.Extensions.AI `IEmbeddingGenerator`
- upsert records (vectors round-trip as JSON arrays)
- fetch a record by key with `GetAsync(key)`
- run a vector similarity search (OpenAI-embedded query) with a LINQ metadata pre-filter
- run filtered (non-vector) retrievals with a variety of LINQ predicates — equality, numeric range, `&&`, `||`, `Contains` (IN), and negation — translated to RedisVL filters via `GetAsync(filter, top)`
- persist and replay chat history with `RedisVLChatMessageStore` on top of `MessageHistory`

## Prerequisites

- .NET 9 SDK
- Redis 8 or another Redis deployment with RediSearch and RedisJSON enabled (JSON storage is used)
- `OPENAI_API_KEY` (the example embeds summaries and the query with OpenAI)
- optional `OPENAI_EMBEDDING_MODEL` (defaults to `text-embedding-3-small`, which produces the 1536-dim vectors the record expects)

Start Redis locally from the repository root if needed:

```bash
docker compose -f docker-compose.integration.yml up -d redis
export REDIS_VL_REDIS_URL=localhost:6379
```

## Run

From the repository root:

```bash
dotnet run --project examples/VectorDataConnectorExample/VectorDataConnectorExample.csproj
```

The example uses `REDIS_VL_REDIS_URL` when it is set, and otherwise falls back to `localhost:6379`.

## Related Docs

- [Examples index](../README.md)
- [Microsoft.Extensions.VectorData connector](../../docs-site/modules/ROOT/pages/extensions/vector-data-connector.adoc)
- [Extensions](../../docs-site/modules/ROOT/pages/extensions/index.adoc)
- [Testing](../../docs-site/modules/ROOT/pages/testing/index.adoc)
