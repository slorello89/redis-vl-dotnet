# Hugging Face Vectorizer Example

This example uses `RedisVL.Vectorizers.HuggingFace` to generate embeddings through the Hugging Face `hf-inference` feature-extraction API and then queries a `SemanticCache` with the resulting vectors.

## Prerequisites

- `HF_TOKEN` set to a Hugging Face token with inference access
- Optional: `HF_EMBEDDING_MODEL` to override the default `intfloat/multilingual-e5-large`
- Optional: `REDIS_VL_REDIS_URL` to point at a Redis Stack instance (defaults to `localhost:6379`)

If `HF_TOKEN` is not set, the example exits immediately with an explicit environment-variable error instead of sending an unauthenticated provider request.

## Run

```bash
dotnet run --project examples/HuggingFaceVectorizerExample/HuggingFaceVectorizerExample.csproj
```

## Related Docs

- [Hugging Face Vectorizer](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/extensions/huggingface-vectorizer.adoc)
- [SemanticCache](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/core-features/semantic-cache.adoc)
- [Examples index](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/README.md)
