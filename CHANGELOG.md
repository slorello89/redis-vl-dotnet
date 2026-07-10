# Changelog

All notable changes to this project are documented here. This project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.0.6] - Unreleased

### Fixed

- **RESP3 result parsing** — `FT.SEARCH`, `FT.AGGREGATE`, and `FT.HYBRID` replies are now parsed
  correctly on connections negotiated over RESP3 (`ConfigurationOptions.Protocol =
  RedisProtocol.Resp3`). Previously the parsers assumed the flat RESP2 reply shape and threw
  `InvalidCastException` against the map-shaped RESP3 replies. The library now detects the reply
  shape and handles both protocols, so no connection needs to be pinned to RESP2. A RESP3 leg was
  added to CI to lock this in (issue #43).

### Added

- **`RedisVL.Vectorizers.Cohere`** — a Cohere v2 `embed`-backed `IBatchTextVectorizer`
  (`CohereTextVectorizer`) with single + batch embedding support, configurable input type
  (`search_document`/`search_query`/`classification`/`clustering`), output dimensionality,
  truncation, optional `X-Client-Name`, and endpoint override. Includes a runnable
  `CohereVectorizerExample` (#26).

### Packaging

- **`RedisVL.Vectorizers.Cohere`** is published to NuGet for the first time. It is marked
  `<IsPackable>true</IsPackable>`, so the existing `nuget-release` workflow ships it automatically.

## [0.0.5] - 2026-06-29

### Added

- **`RedisVL.Connectors.VectorData`** — a Microsoft.Extensions.VectorData (MEVD) / Semantic Kernel
  vector-store connector backed by RedisVL: `RedisVLVectorStore`, `VectorStoreCollection<TKey, TRecord>`,
  a LINQ→`FilterExpression` mapper, and `RedisVLChatMessageStore` (issue #11, #18).
- **SVS-VAMANA vector index + vector compression** — `VectorAlgorithm.SvsVamana`, `VectorCompression`
  (LVQ4/4x4/4x8/8, LeanVec4x8/8x8), build-time knobs (`graphMaxDegree`, `constructionWindowSize`,
  `searchWindowSize`, `epsilon`, `trainingThreshold`, `reduce`), and query-time runtime knobs
  (`searchWindowSize`, `useSearchHistory`, `searchBufferCapacity`) on `VectorKnnRuntimeOptions`
  (issue #12 gap #5, #19).
- Vector field emission/parsing for the `FLOAT16`, `BFLOAT16`, `UINT8`, and `INT8` data types.
- `HybridSearchExample` — a runnable sample for native `FT.HYBRID` search (#20).

### Packaging

- The `nuget-release` workflow now packs every publishable project from the solution
  (`<IsPackable>true</IsPackable>`) instead of a hardcoded list. This publishes
  **`RedisVL.Connectors.VectorData`**, **`RedisVL.Vectorizers.ExtensionsAI`**, and
  **`RedisVL.Rerankers.Onnx`** to NuGet for the first time, and prevents future opt-in packages
  from being silently omitted from releases.

## [0.0.4] and earlier

Earlier releases were published to NuGet without a tracked changelog. See the Git history for details.
