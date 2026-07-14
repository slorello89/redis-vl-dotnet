# Changelog

All notable changes to this project are documented here. This project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Removed

- **BREAKING: the entire synchronous API surface has been removed** — all 119 sync wrapper methods
  (implemented as `...Async(...).GetAwaiter().GetResult()`) across `SearchIndex`, `EmbeddingsCache`,
  `SemanticCache`, `SemanticRouter`, `MessageHistory`, and `SemanticMessageHistory` — including the
  sync `Lookup`/`LookupEmbedding`/`LookupMany` convenience methods on `EmbeddingsCache` — are gone.
  Sync-over-async blocks thread-pool threads under load and risks deadlock under custom
  synchronization contexts; call the `*Async` equivalents instead. Genuinely synchronous members
  (e.g. `SemanticCache.ResetStatistics`) are unaffected (issue #41).
- **BREAKING: `ITextEmbeddingGenerator` has been removed** — the `[Obsolete]`
  `RedisVL.Caches.ITextEmbeddingGenerator` shim (unused since the vectorizer abstractions were
  consolidated) has been deleted along with its `RedisVL.Caches` interface. Use
  `RedisVL.Vectorizers.ITextVectorizer` from `RedisVL.Vectorizers.Abstractions` instead (issue #51).
- **BREAKING: `GeoFilterField.WithinBox` has been removed** — it emitted
  `@field:[minLon minLat maxLon maxLat]`, a syntax no RediSearch server accepts: GEO fields only
  support the radius query form (`[lon lat radius unit]`), and bounding-box queries require GEOSHAPE
  fields, which this library does not model. Every call always threw a syntax error server-side, so
  the API could never succeed against any server. `WithinRadius` is unaffected and still supported
  (PR #82).
- **BREAKING: Redis Sentinel connection support** — `RedisConnectionFactory` no longer exposes
  `CreateSentinelOptions`, `ConnectSentinelAsync`, or `ConnectSentinelPrimaryAsync` (nor the
  `DefaultSentinelPort` constant). Redis 8 bundles the modules redis-vl needs and the supported
  topologies are standalone and cluster; the Sentinel-specific API, tests, example branch, and docs
  have been removed. Use a standalone or cluster connection instead.

### Fixed

- **Cache write hardening** — `SemanticCache.StoreAsync` and `SemanticRouter`'s reference writes now
  validate `embedding.Length` against the field's configured dimensions before writing, and throw
  `ArgumentException` on a mismatch, instead of silently writing a hash that RediSearch fails to index
  (a permanent 0% hit-rate with no error). Re-storing a key/reference without an optional field (e.g.
  metadata, or a route's per-reference threshold) now clears the previously-stored value for that
  field in the same operation instead of leaving it merged in from the prior write; the HSET, the
  stale-field clear, and the TTL `EXPIRE` are now issued together in a single MULTI/EXEC transaction
  (previously separate awaits), so a cancellation between them can no longer leave an immortal
  (no-TTL) entry (issue #47).
- **Enum properties on hash documents now round-trip** — `HashDocumentMapper` and `SearchResultMapper`
  reconstructed a stored enum value as a JSON string token, but `System.Text.Json`'s default enum
  converter only reads number tokens unless a string converter is registered, so every typed
  fetch/search of a POCO with an enum property threw `JsonException`. Both mappers now parse the
  stored value with `Enum.TryParse` (accepting both numeric strings and member names) and re-serialize
  it through the caller's `JsonSerializerOptions`, so whichever enum converter the caller uses
  round-trips correctly (PR #84).
- **`SemanticRouter.RouteAsync` and `SemanticMessageHistory.GetRelevantAsync` now honor custom
  `*FieldName` options** — both methods mapped results through a private typed record whose property
  names only lined up with the *default* field names, so a caller using a custom field name (e.g.
  `SemanticRouterOptions(routeNameFieldName: "route")`) got a `SearchResultMappingException` on every
  match even though the data came back correctly. Both now extract each field by its configured
  `Options.*FieldName`, matching the sibling methods (`RouteManyAsync`, `MessageHistory.GetRecentAsync`)
  that already did this correctly; behavior for default field names is unchanged (PR #83).
- **`SearchBatchesAsync` clones now preserve full query state** — the per-page query clone dropped
  `FilterQuery.SortBy` and `TextQuery.FieldWeights`, so batched/paged queries silently ignored a
  requested sort order or per-field text weights that single-shot `SearchAsync` honors. Every
  constructor parameter is now threaded through both clones (PR #81).
- **All vector query types omit `RETURN` when return fields are unspecified, including across batch
  pages** — `VectorRangeQuery` and `HybridQuery` unconditionally normalized `returnFields` to just the
  score/distance alias, and `MultiVectorQuery`'s fan-out sub-queries did the same, so a default
  (unprojected) query returned only the score and typed mapping threw on a POCO's other properties.
  Batch paging additionally lost the "unspecified" sentinel on each cloned page, so even a default
  `VectorQuery` regressed once paged. `VectorQuery`, `VectorRangeQuery`, `HybridQuery`, and
  `MultiVectorQuery` now all omit `RETURN` consistently, including across batch-clone pages (PR #85;
  completes the issue #54 fix below, which previously covered only single-shot `VectorQuery`).
- **More pre-release audit edge cases** — a second batch of small correctness fixes from the audit
  (issue #54): a default `VectorQuery` (no `returnFields`) now omits `RETURN` so the server returns
  all stored fields plus the yielded score — the obvious `SearchAsync<T>(new VectorQuery(...))` happy
  path no longer throws on a typed document's non-nullable properties; `Filter.Tag(...).Like(...)`
  now renders patterns with the `w'...'` form so `?` acts as a single-character wildcard (plain
  `{...}` tag syntax takes `?` literally and silently matched nothing); `SemanticCache` now throws a
  descriptive error when a matched document cannot be mapped (schema drift) instead of silently
  counting it as a miss; `RedisConnectionFactory.ConnectClusterAsync` disposes the multiplexer if a
  cancelled connect later succeeds, rather than leaking it; and `SearchResults` now exposes a
  `Warnings` collection populated from the `FT.HYBRID` reply (previously discarded).
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

- **`SemanticRouter` per-route config storage redesign** — per-route distance thresholds and metadata
  now live in a dedicated per-route config key instead of being denormalized onto every reference
  document, so a route's effective threshold no longer depends on which reference happens to be
  nearest at query time. `RouteAsync` and `RouteManyAsync` now resolve thresholds identically
  ("per-route value capped by the router-wide default"). **Migration note:** per-route thresholds
  stored by earlier versions on reference documents are not read by the new code — after upgrading,
  existing routes fall back to the router-wide default threshold until the route's threshold is
  re-added (issue #48).
- **VectorData connector: unsupported/mistranslated filter constructs now throw** — the LINQ filter
  translator previously emitted TAG-filter syntax unconditionally for `Contains`, silently producing
  wrong queries (numeric `IN` matched nothing; scalar-string/text `Contains` used the wrong semantics).
  It now dispatches on the resolved property kind: numeric `Contains` translates to an OR of numeric
  equalities, TAG-collection membership is unchanged, and scalar-string `string.Contains` / Text-kind
  `Contains` now throw `NotSupportedException` instead of emitting an incorrect query (PR #81).
- **Batch write failure semantics** — because batch writes are now dispatched concurrently, the
  `*Many` / bulk-load methods are no longer implicitly all-or-nothing on the first failure. Inputs are
  validated up front (so a malformed request fails the whole call before any command is issued), but a
  Redis-level failure mid-batch may leave siblings dispatched alongside it already applied; batches are
  not transactional and are not rolled back. Results/keys remain aligned to input order (issue #44).

### Added

- **Six public interfaces as mocking seams** — `ISearchIndex`, `IEmbeddingsCache`, `ISemanticCache`,
  `ISemanticRouter`, `IMessageHistory`, and `ISemanticMessageHistory` let consumers substitute test
  doubles for the core service types without hand-rolled wrappers. The concrete classes stay sealed
  but now implement these interfaces; static factories and internal helpers remain off the interfaces,
  so the change is additive and non-breaking for code using the concrete types (issue #51).
- **net8.0 and net10.0 target frameworks** — packable libraries now multi-target
  `net8.0;net9.0;net10.0` (previously net9.0 only, an STS release), so LTS (net8.0) and net10.0
  consumers can install the packages; example apps remain single-target net9.0 (commit 54675af).
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
