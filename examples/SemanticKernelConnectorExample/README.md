# Semantic Kernel Connector Example

This example is a runnable .NET 9 console app that shows **Semantic Kernel consuming the RedisVL
`Microsoft.Extensions.VectorData` (MEVD) connector**. Because Semantic Kernel's vector-store and
text-search abstractions are built on MEVD, the same `RedisVLVectorStore` / `RedisVLCollection` from
the [VectorDataConnectorExample](../VectorDataConnectorExample/README.md) plugs straight into SK's
`VectorStoreTextSearch<T>` — there is no Redis-specific Semantic Kernel connector to install.

It shows:

- create a `RedisVLVectorStore` and a typed `VectorStoreCollection<string, Movie>`
- seed records, embedding each summary with OpenAI through a Microsoft.Extensions.AI `IEmbeddingGenerator`
- wrap the RedisVL collection in SK's `VectorStoreTextSearch<Movie>`
- run `GetTextSearchResultsAsync(...)` — SK embeds the query and searches RedisVL (RAG retrieval)
- expose the search to a `Kernel` as a plugin with `CreateWithGetTextSearchResults(...)`

The same OpenAI `IEmbeddingGenerator` is shared by the collection and SK, so document and query
embeddings come from the same model. Swap in any other `IEmbeddingGenerator<string, Embedding<float>>`
(Azure OpenAI, local ONNX, etc.) as needed.

## Prerequisites

- .NET 9 SDK
- Redis Stack or another Redis deployment with RediSearch and RedisJSON enabled (JSON storage is used)
- `OPENAI_API_KEY` (embeds documents and queries with OpenAI)
- optional `OPENAI_EMBEDDING_MODEL` (defaults to `text-embedding-3-small`, which produces the 1536-dim vectors the record expects)

Start Redis locally from the repository root if needed:

```bash
docker compose -f docker-compose.integration.yml up -d redis-stack
export REDIS_VL_REDIS_URL=localhost:6379
```

## Run

From the repository root:

```bash
dotnet run --project examples/SemanticKernelConnectorExample/SemanticKernelConnectorExample.csproj
```

The example uses `REDIS_VL_REDIS_URL` when it is set, and otherwise falls back to `localhost:6379`.

## Version note

Semantic Kernel 1.77.0 builds against `Microsoft.Extensions.VectorData.Abstractions` 10.1.0, so the
`RedisVL.Connectors.VectorData` package targets that version as its floor. A connector built against a
newer MEVD (which added a `class` constraint to `VectorSearchResult<T>`) is binary-incompatible with
SK 1.77 at runtime.

## Related Docs

- [Examples index](../README.md)
- [Microsoft.Extensions.VectorData connector](../../docs-site/modules/ROOT/pages/extensions/vector-data-connector.adoc)
- [VectorDataConnectorExample](../VectorDataConnectorExample/README.md)
