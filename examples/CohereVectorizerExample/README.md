# Cohere Vectorizer Example

This example uses `RedisVL.Vectorizers.Cohere` to generate embeddings through the Cohere v2 `embed` API and then queries a `SemanticCache` with the resulting vectors. It also shows Cohere's `input_type` distinction: documents are embedded with `SearchDocument` and the lookup query is embedded with `SearchQuery`.

## Prerequisites

- `COHERE_API_KEY` set to a Cohere API key
- Optional: `COHERE_EMBEDDING_MODEL` to override the default `embed-english-v3.0`
- Optional: `COHERE_MATCH_THRESHOLD` to override the cosine-distance cutoff used to accept a match (defaults to `0.7`)
- Optional: `REDIS_VL_REDIS_URL` to point at a Redis Stack instance (defaults to `localhost:6379`)

> Cohere v3 uses asymmetric `search_query` / `search_document` embeddings, so a strong match typically lands around `0.5`–`0.65` cosine distance — larger than you'd see from a normalized OpenAI/Hugging Face model. The example always prints the nearest distance so you can tune `COHERE_MATCH_THRESHOLD` to your data.

If `COHERE_API_KEY` is not set, the example exits immediately with an explicit environment-variable error instead of sending an unauthenticated provider request.

## Run

```bash
dotnet run --project examples/CohereVectorizerExample/CohereVectorizerExample.csproj
```

## Related Docs

- [Cohere Vectorizer](../../docs-site/modules/ROOT/pages/extensions/cohere-vectorizer.adoc)
- [SemanticCache](../../docs-site/modules/ROOT/pages/core-features/semantic-cache.adoc)
- [Examples index](../README.md)
