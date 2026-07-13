# Changelog

All notable changes to this project are documented here. This project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.0.6] - Unreleased

### Removed

- **BREAKING: Redis Sentinel connection support** — `RedisConnectionFactory` no longer exposes
  `CreateSentinelOptions`, `ConnectSentinelAsync`, or `ConnectSentinelPrimaryAsync` (nor the
  `DefaultSentinelPort` constant). Redis 8 bundles the modules redis-vl needs and the supported
  topologies are standalone and cluster; the Sentinel-specific API, tests, example branch, and docs
  have been removed. Use a standalone or cluster connection instead.

### Fixed

- **Query-construction and validation edge cases** — a batch of small correctness fixes from the
  pre-release audit (issue #54): `Filter.Text(...).Match`/`Prefix` now escape `*` so an exact-term
  match stays literal instead of silently becoming a prefix wildcard and `Prefix("foo*")` no longer
  emits the invalid `foo**`; `Filter.Numeric(...).Between`/`Eq` reject `NaN` bounds up front instead
  of failing at the server; `VectorRangeQuery` now accepts a `distanceThreshold` of `0` (a valid
  exact-duplicate radius) and rejects only negative or `NaN` thresholds; the `FromFloat32`/`FromFloat64`
  factories on every query type throw `ArgumentNullException` (not `ArgumentException`) on a null
  vector; the `Vector` getter on every query type returns a defensive copy so callers cannot mutate
  query state; and the `alias` passed to `AggregationApply`/`AggregationReducer` is trimmed before its
  leading `@` is stripped, so `" @score"` normalizes to `score`.
- **Schema model: HNSW `EPSILON` and vector `INDEXMISSING`** — `VectorFieldAttributes` no longer
  rejects a positive `epsilon` for HNSW fields; `EPSILON` is a documented HNSW/SVS-VAMANA range-query
  approximation factor and is still rejected for FLAT (matching Redis). `FT.CREATE` now emits the
  field-level `INDEXMISSING` token for vector fields (after the counted algorithm attributes) when
  `VectorFieldDefinition.IndexMissing` is set, so declared schemas are no longer silently dropped and
  `FromExisting`/create round-trips stay symmetric (issue #50).
- **RESP3 result parsing** — `FT.SEARCH`, `FT.AGGREGATE`, and `FT.HYBRID` replies are now parsed
  correctly on connections negotiated over RESP3 (`ConfigurationOptions.Protocol =
  RedisProtocol.Resp3`). Previously the parsers assumed the flat RESP2 reply shape and threw
  `InvalidCastException` against the map-shaped RESP3 replies. The library now detects the reply
  shape and handles both protocols, so no connection needs to be pinned to RESP2. A RESP3 leg was
  added to CI to lock this in (issue #43).

### Performance

- **Bulk and batch APIs now pipeline** — every bulk/batch path used to `await` one command per item,
  paying a full network round trip before issuing the next (10k documents at 1 ms RTT ≈ 10 s). They
  now dispatch commands in bounded batches and let StackExchange.Redis pipeline them over the
  multiplexer, collapsing a batch to roughly a single round trip. Affected: `SearchIndex.LoadJsonAsync`
  / `LoadHashAsync` (batch overloads) and `SearchAsync(MultiVectorQuery)`;
  `SemanticCache.CheckManyAsync` / `StoreManyAsync`; every `EmbeddingsCache` `*Many` method;
  `SemanticRouter.AddRouteAsync(Route, …)` / `AddRouteReferencesAsync`; and
  `RedisVLCollection.UpsertAsync(IEnumerable<TRecord>)` (issue #44).
- **Batch delete methods issue one round trip** — `EmbeddingsCache.DeleteManyAsync` /
  `DeleteManyByKeyAsync` now pipeline single-key deletes (cluster-slot-safe, matching
  `SearchIndex.ClearAsync`) instead of awaiting each delete serially.

### Changed

- **Batch write failure semantics** — because batch writes are now dispatched concurrently, the
  `*Many` / bulk-load methods are no longer implicitly all-or-nothing on the first failure. Inputs are
  validated up front (so a malformed request fails the whole call before any command is issued), but a
  Redis-level failure mid-batch may leave siblings dispatched alongside it already applied; batches are
  not transactional and are not rolled back. Results/keys remain aligned to input order (issue #44).

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
