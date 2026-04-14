# Semantic Router Example

Demonstrates nearest-route matching:

- create a `SemanticRouter` with a local sample vectorizer
- add multiple routes with reference text
- route a new utterance to the nearest stored route
- clean up the router index and documents

## Prerequisites

- .NET 9 SDK
- Redis Stack or another Redis deployment with RediSearch enabled
- Optional: `REDIS_VL_REDIS_URL` to point at a Redis instance other than `localhost:6379`

Redis prerequisites:

- RediSearch with vector similarity support

Start Redis locally from the repository root if needed:

```bash
docker compose -f docker-compose.integration.yml up -d redis-stack
export REDIS_VL_REDIS_URL=localhost:6379
```

This example uses a local `KeywordVectorizer`, so it does not require OpenAI, Hugging Face, or Cohere credentials.

## Run

Run it from the repository root:

```bash
dotnet run --project examples/SemanticRouterExample/SemanticRouterExample.csproj
```

## Related Docs

- [Examples index](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/README.md)
- [SemanticRouter](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/core-features/semantic-router.adoc)
- [Getting Started](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/getting-started/index.adoc)
- [Testing](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/testing/index.adoc)
