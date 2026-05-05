# Microsoft.Extensions.AI Vectorizer Example

This example shows how to wrap an OpenAI-backed `Microsoft.Extensions.AI` embedding generator with `RedisVL.Vectorizers.ExtensionsAI`.

## Prerequisites

- .NET 9 SDK
- `OPENAI_API_KEY`
- Optional: `OPENAI_EMBEDDING_MODEL` to override the default `text-embedding-3-small`
- Optional: `OPENAI_EMBEDDING_DIMENSIONS` to request a reduced embedding size when the selected model supports it

This example does not require Redis. It creates an `OpenAI.Embeddings.EmbeddingClient`, converts it to `IEmbeddingGenerator<string, Embedding<float>>` with `AsIEmbeddingGenerator(...)`, and then adapts that generator to `RedisVL` with `ExtensionsAiTextVectorizer`.

## Run

```bash
dotnet run --project examples/ExtensionsAiVectorizerExample/ExtensionsAiVectorizerExample.csproj
```

## Related Docs

- [Microsoft.Extensions.AI Vectorizer](../../docs-site/modules/ROOT/pages/extensions/extensions-ai-vectorizer.adoc)
- [Vectorizer Abstractions](../../docs-site/modules/ROOT/pages/extensions/vectorizer-abstractions.adoc)
- [Examples index](../README.md)
