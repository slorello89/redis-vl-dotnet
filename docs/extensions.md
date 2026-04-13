# Extension Packages

`redis-vl-dotnet` now separates text vectorizer contracts from the core Redis package so provider integrations can live in optional extension packages.

## Package Layout

- `src/RedisVlDotNet`: core Redis search, schema, cache, and workflow APIs
- `src/RedisVlDotNet.Vectorizers.Abstractions`: provider-agnostic text vectorizer contracts consumed by the core package
- `src/RedisVlDotNet.Vectorizers.OpenAI`: scaffold for a provider-specific vectorizer package that can add vendor SDK dependencies without flowing them into the core assembly

## Contracts

- `ITextVectorizer`: single-input text to embedding contract for caches and semantic workflows
- `IBatchTextVectorizer`: optional batch contract for providers that can embed multiple inputs in one request
- `TextVectorizerExtensions.VectorizeManyAsync(...)`: shared fallback that preserves input order and lets callers batch against either contract

## Compatibility

The legacy `RedisVlDotNet.Caches.ITextEmbeddingGenerator` interface remains as an obsolete shim over `ITextVectorizer` so existing applications can migrate incrementally.

New provider integrations should target the abstractions package directly rather than implementing core-package types.
