# Hugging Face Vectorizer Example

This example uses `RedisVlDotNet.Vectorizers.HuggingFace` to generate embeddings through the Hugging Face `hf-inference` feature-extraction API and then queries a `SemanticCache` with the resulting vectors.

## Prerequisites

- `HF_TOKEN` set to a Hugging Face token with inference access
- Optional: `HF_EMBEDDING_MODEL` to override the default `intfloat/multilingual-e5-large`
- Optional: `REDIS_VL_REDIS_URL` to point at a Redis Stack instance (defaults to `localhost:6379`)

## Run

```bash
dotnet run --project examples/HuggingFaceVectorizerExample/HuggingFaceVectorizerExample.csproj
```
