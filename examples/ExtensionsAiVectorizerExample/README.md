# Microsoft.Extensions.AI Vectorizer Example

This example shows how to wrap a `Microsoft.Extensions.AI` embedding generator with `RedisVL.Vectorizers.ExtensionsAI`.

## Prerequisites

- .NET 9 SDK

This example uses an in-memory sample `IEmbeddingGenerator<string, Embedding<float>>`, so it does not require provider credentials or Redis.

## Run

```bash
dotnet run --project examples/ExtensionsAiVectorizerExample/ExtensionsAiVectorizerExample.csproj
```

## Related Docs

- [Microsoft.Extensions.AI Vectorizer](../../docs-site/modules/ROOT/pages/extensions/extensions-ai-vectorizer.adoc)
- [Vectorizer Abstractions](../../docs-site/modules/ROOT/pages/extensions/vectorizer-abstractions.adoc)
- [Examples index](../README.md)
