# Extension Packages

`redis-vl-dotnet` now separates AI-adjacent contracts from the core Redis package so provider integrations can live in optional extension packages.

## Package Layout

- `src/RedisVlDotNet`: core Redis search, schema, cache, and workflow APIs
- `src/RedisVlDotNet.Vectorizers.Abstractions`: provider-agnostic text vectorizer contracts consumed by the core package
- `src/RedisVlDotNet.Rerankers.Abstractions`: provider-agnostic reranker contracts for query-plus-candidate reranking flows
- `src/RedisVlDotNet.Vectorizers.HuggingFace`: Hugging Face `hf-inference` feature-extraction package built on `HttpClient`
- `src/RedisVlDotNet.Vectorizers.OpenAI`: OpenAI-backed vectorizer package that adds the vendor SDK without flowing it into the core assembly
- `src/RedisVlDotNet.Rerankers.Cohere`: scaffold package reserved for a Cohere-backed reranker implementation in a follow-up story

## Contracts

- `ITextVectorizer`: single-input text to embedding contract for caches and semantic workflows
- `IBatchTextVectorizer`: optional batch contract for providers that can embed multiple inputs in one request
- `TextVectorizerExtensions.VectorizeManyAsync(...)`: shared fallback that preserves input order and lets callers batch against either contract
- `ITextReranker`: query-plus-candidate reranking contract that returns scored results in provider-selected order
- `RerankRequest`: immutable request DTO with the query text, candidate documents, and optional `TopN`
- `RerankDocument` / `RerankResult`: shared candidate and scored-result contracts for reranker packages
- `TextRerankerExtensions.RerankAsync(...)`: convenience overload for reranking plain string candidates without building request DTOs by hand

## Compatibility

The legacy `RedisVlDotNet.Caches.ITextEmbeddingGenerator` interface remains as an obsolete shim over `ITextVectorizer` so existing applications can migrate incrementally.

New provider integrations should target the abstractions package directly rather than implementing core-package types.

## OpenAI Package

`RedisVlDotNet.Vectorizers.OpenAI` provides:

- `OpenAiTextVectorizer`: an `IBatchTextVectorizer` implementation backed by the OpenAI .NET `EmbeddingClient`
- `OpenAiVectorizerOptions`: per-request embedding options for dimensions and end-user ids

The package supports both single-text and multi-text embedding flows and can be plugged directly into APIs like `SemanticCache`, `SemanticRouter`, and `SemanticMessageHistory`.

## Hugging Face Package

`RedisVlDotNet.Vectorizers.HuggingFace` provides:

- `HuggingFaceTextVectorizer`: an `IBatchTextVectorizer` implementation that posts feature-extraction requests to Hugging Face's `hf-inference` router
- `HuggingFaceVectorizerOptions`: optional request settings for normalization, prompt selection, truncation, and custom endpoint overrides

The package supports both single-text and multi-text embedding flows and can be plugged directly into APIs like `SemanticCache`, `SemanticRouter`, and `SemanticMessageHistory`.

## Reranker Package Scaffolding

`RedisVlDotNet.Rerankers.Abstractions` defines the shared reranking boundary outside the core package. Provider packages should accept a search query plus a candidate document set, then return `RerankResult` instances that preserve the original candidate document payload alongside provider scores.

`RedisVlDotNet.Rerankers.Cohere` is currently a scaffold package only. It exists so the provider-specific implementation can be added in the next roadmap story without changing the solution or package layout again.
