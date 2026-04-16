# Message History Example

Demonstrates semantic message history retrieval:

- create a semantic message history index with a vector field
- append session messages with generated embeddings and metadata payloads
- retrieve the most recent messages for a session
- retrieve semantically relevant messages for a prompt within the same session
- clean up the example index and documents

## Prerequisites

- .NET 9 SDK
- Redis Stack or another Redis deployment with RediSearch enabled
- Optional: `REDIS_VL_REDIS_URL` to point at a Redis instance other than `localhost:6379`

Start Redis locally from the repository root if needed:

```bash
docker compose -f docker-compose.integration.yml up -d redis-stack
export REDIS_VL_REDIS_URL=localhost:6379
```

This example uses a local `KeywordEmbeddingGenerator`, so it does not require OpenAI, Hugging Face, or Cohere credentials.

## Run

Run it from the repository root:

```bash
dotnet run --project examples/MessageHistoryExample/MessageHistoryExample.csproj
```

## Related Docs

- [Examples index](../README.md)
- [SemanticMessageHistory](../../docs-site/modules/ROOT/pages/core-features/semantic-message-history.adoc)
- [Getting Started](../../docs-site/modules/ROOT/pages/getting-started/index.adoc)
- [Testing](../../docs-site/modules/ROOT/pages/testing/index.adoc)
