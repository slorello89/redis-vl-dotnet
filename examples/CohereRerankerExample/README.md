# Cohere Reranker Example

This example combines a Redis text search with `RedisVl.Rerankers.Cohere` so the top Redis candidates can be reranked by Cohere for better relevance.

## Prerequisites

- `COHERE_API_KEY`
- Optional: `COHERE_RERANK_MODEL` to override the default `rerank-v4.0-pro`
- Optional: `REDIS_VL_REDIS_URL` to point at a Redis Stack instance (defaults to `localhost:6379`)

If `COHERE_API_KEY` is not set, the example exits immediately with an explicit environment-variable error instead of attempting the rerank request.

## Run

```bash
dotnet run --project examples/CohereRerankerExample/CohereRerankerExample.csproj
```
