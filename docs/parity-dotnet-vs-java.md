# RedisVL: .NET vs Java Parity Diff

Date: 2026-06-08

A point-in-time feature comparison between `redis-vl-dotnet` and `redis-vl-java`, built by inventorying the actual public source of both libraries (not their READMEs).

## Versions compared

| | `redis-vl-dotnet` | `redis-vl-java` |
| --- | --- | --- |
| Version | 0.0.4 | 0.13.1 (last commit 2026-06-05) |
| Runtime | .NET 9 (`net9.0`) | Java 17 (toolchain 21) |
| Redis client | StackExchange.Redis | Jedis 7.3.0 |
| Packaging | 8 NuGet packages (core + opt-in provider extensions) | single Maven artifact `com.redis:redisvl` (providers `compileOnly`) |
| API shape | async-first (`Task` + `CancellationToken`) with sync wrappers | synchronous |
| Result handling | typed record/POCO mapping (`Map<T>`) as default | `Map<String,Object>` / Jedis `SearchResult` oriented |

> Caveat: `redis-vl-java`'s checked-in `FEATURE_PARITY_REPORT.md` and README are stale/self-contradictory (reference non-existent types, list `getRelevant()` and `VoyageAIReranker` as both done and missing, cite "~95% parity / 0.1.0" while the repo is at 0.13.1). This diff is based on the Java source tree, not those documents.

## Capability matrix

Legend: ✅ present · ⚠️ partial/limited · ❌ absent

| Capability | .NET | Java | Notes |
| --- | --- | --- | --- |
| Core schema model (text/tag/numeric/geo/vector) | ✅ | ✅ | Parity on baseline field model. |
| YAML schema loading | ✅ | ✅ | Java also loads from JSON/Map and serializes back (`toYaml/toJson`). |
| Multiple prefixes / stopwords / key separator | ✅ | ✅ | Parity. |
| Vector algorithms | ✅ Flat, HNSW, SVS-VAMANA | ✅ Flat, HNSW, SVS-VAMANA | Parity. .NET added SVS-VAMANA + vector compression (LVQ, LeanVec) in issue #12. |
| Vector data types | ✅ float32/64, float16, bfloat16, int8, uint8 | ✅ same set | Parity. |
| Index lifecycle (create/exists/info/drop) | ✅ | ✅ | Parity. |
| Clear / list / from-existing | ✅ | ✅ | Parity. |
| Document load/fetch/delete | ✅ | ✅ | Parity. |
| Partial updates (HASH + JSON) | ✅ | ✅ | Parity. |
| Filter query | ✅ | ✅ | Parity. |
| Vector (KNN) query | ✅ | ✅ | Parity. |
| Vector range query (+ epsilon) | ✅ | ✅ | Parity; Java adds SVS runtime knobs. |
| Count query | ✅ | ✅ | Parity. |
| Text query | ✅ | ✅ | Java adds per-field weights (`textFieldWeights`). |
| Aggregation query | ✅ | ✅ | Parity. |
| Multi-vector query | ✅ | ✅ | Parity. |
| Hybrid query (FT.SEARCH form) | ✅ | ✅ | Parity. |
| Aggregate-hybrid (FT.AGGREGATE) | ✅ | ✅ | Parity. |
| **Native `FT.HYBRID` (Redis 8.4+)** | ✅ `HybridSearchQuery` | ✅ `HybridQuery` | Recently added in .NET (PR #5). Java auto-falls back to FT.AGGREGATE for pre-8.4 (`toAggregateHybridQuery()`); .NET keeps the two as separate types. |
| Pagination / batch query helpers | ✅ | ✅ | Parity. |
| Filter expression API (tag/text/numeric/geo, boolean) | ✅ | ✅ | Java is richer: `timestamp`, `fuzzy`, `wildcard`, `prefix`, `box` geo, `tagLike`. |
| Runtime vector params | ✅ efRuntime, epsilon, SVS knobs | ✅ same set | Parity. .NET added searchWindowSize / useSearchHistory / searchBufferCapacity (SVS) in issue #12. |
| Built-in vectorizers | ✅ OpenAI, HuggingFace, Microsoft.Extensions.AI | ✅ LangChain4J pass-through (OpenAI/Azure/Cohere/HF/Ollama/Vertex/Mistral/Voyage), local SentenceTransformers (ONNX) | Different strategy: .NET ships discrete provider packages + a MEAI adapter; Java funnels everything through LangChain4J + a local ONNX embedder. |
| Local/offline embeddings | ❌ (no ONNX *embedder*) | ✅ `SentenceTransformersVectorizer` | .NET has a local ONNX *reranker* but no local embedder. |
| Built-in rerankers | ✅ Cohere, local ONNX | ✅ Cohere, local ONNX (HFCrossEncoder), **VoyageAI** | Java adds VoyageAI. |
| Embeddings cache | ✅ | ✅ | Parity. |
| Semantic cache | ⚠️ store/check/delete | ✅ + hit-rate stats, `checkTopK`, batch store/check, `update` | Java richer. |
| LangCache-style integration | ❌ | ✅ `LangCacheSemanticCache` | Java integrates a hosted LangCache server. |
| Semantic router | ⚠️ single best match | ✅ multi-match (`routeMany`), multiple references, `RoutingConfig`, distance aggregation methods | Java richer. |
| Message history | ✅ | ✅ | Parity (Java adds role enum/tool-call metadata, `getRecent`). |
| Semantic message history | ✅ | ✅ | Parity. |
| Framework adapters | ✅ Microsoft.Extensions.AI embeddings + Microsoft.Extensions.VectorData (MEVD) vector-store connector & chat-memory store | ✅ LangChain4J `EmbeddingStore`/`ContentRetriever`/`DocumentStore`/`ChatMemoryStore` + filter mapper | .NET now ships `RedisVL.Connectors.VectorData` (`VectorStore`/`VectorStoreCollection`, LINQ filter mapper, `RedisVLChatMessageStore`), consumable by Semantic Kernel since SK builds on MEVD. |
| Extractive summarization | ✅ `ExtractiveSelector` + `SentenceSplitter` | ✅ `ExtractiveSelector` + sentence splitter | Parity. .NET added in issue #12 (embedding + k-means++ selection; rule-based splitter, no NLP model dependency). |
| VCR record/replay test harness | ❌ | ✅ `com.redis.vl.test.vcr` (shipped in main jar) | Java-only; records LLM/embedding calls to Redis. |
| Connection: standalone / cluster / sentinel | ✅ | ✅ | Parity (different clients). |
| CLI | ✅ `RedisVL.Cli` | ❌ none | **.NET ahead** — Java ships no CLI. |

## Where .NET leads

- **CLI** — `RedisVL.Cli` (create/list/info/clear/delete index, load YAML schema). Java has no CLI module at all.
- **Typed result mapping** as the default workflow (records/POCOs via `Map<T>`), vs Java's map/`SearchResult` orientation.
- **Async-first** idiomatic API with `CancellationToken` on every I/O call.
- **Microsoft.Extensions.AI** integration — idiomatic .NET embedding abstraction (`IEmbeddingGenerator`).
- **Modular packaging** — provider dependencies are opt-in NuGet packages rather than a single artifact with `compileOnly` providers.

## Where Java leads (gaps for .NET)

Roughly in priority order for a .NET consumer:

1. **Richer semantic cache** — hit/miss statistics, `checkTopK`, batch store/check, `update`.
2. **Richer semantic router** — `routeMany` (multiple matches), multiple references per route, `RoutingConfig`, distance-aggregation methods.
3. **Filter breadth** — `timestamp` helper, `fuzzy`, `wildcard`, `box` geo, `tagLike`; per-field text weighting (`textFieldWeights`).
4. **Local/offline embeddings** — Java has a SentenceTransformers (ONNX) vectorizer; .NET only has a local ONNX *reranker*.
5. ~~**Vector index breadth** — SVS-VAMANA algorithm + vector compression (LVQ / LeanVec) and the associated runtime knobs.~~ **Closed (issue #12, gap #5):** `VectorAlgorithm.SvsVamana` + `VectorCompression` (LVQ4/4x4/4x8/8, LeanVec4x8/8x8) and build/runtime knobs (`graphMaxDegree`, `constructionWindowSize`, `searchWindowSize`, `epsilon`, `trainingThreshold`, `reduce`; query-time `searchWindowSize` / `useSearchHistory` / `searchBufferCapacity`).
6. **LangCache integration** (`LangCacheSemanticCache`) — already tracked as "Deferred" in the older roadmap.
7. **VoyageAI reranker**.
8. ~~**Framework/ecosystem hooks** — Java's LangChain4J adapters (EmbeddingStore, ContentRetriever, DocumentStore, ChatMemoryStore).~~ **Closed:** `RedisVL.Connectors.VectorData` ships a Microsoft.Extensions.VectorData `VectorStore`/`VectorStoreCollection` connector (consumable by Semantic Kernel), a LINQ→`FilterExpression` mapper, and a `RedisVLChatMessageStore` on top of `MessageHistory`.
9. ~~**Extractive summarization** utilities.~~ **Closed (issue #12, gap #9):** the opt-in `RedisVL.Summarization` package ships `ExtractiveSelector` (embedding + k-means++ key-sentence selection) and a rule-based `SentenceSplitter` — no NLP-model dependency.
10. **VCR-style test harness** for deterministic LLM/embedding tests.

## Intentional .NET-native differences (not gaps)

- Async-first surface with `CancellationToken` instead of a separate sync/async split.
- Typed record/POCO mapping as the default result model.
- Provider AI dependencies shipped as optional extension packages rather than in the core assembly.
- Idiomatic .NET naming/construction (factory methods, records, fluent filters) over a literal API clone.

## Bottom line

The two libraries are at **near-parity on the core vector-search surface** — schema, index lifecycle, document CRUD, the full query set (including native `FT.HYBRID`, which .NET just added), filters, embeddings cache, the four workflow primitives, and cluster/sentinel connectivity. **.NET is ahead on CLI, typed mapping, async ergonomics, and modular packaging.**

Java is the more mature, broader **platform** (0.13.1 vs 0.0.4): it leads on AI-ecosystem breadth (LangChain4J adapters, more vectorizer/reranker providers, local embeddings), richer cache/router feature depth, filter breadth, and extras like LangCache and a VCR test harness. None of those are core-search gaps — they're platform-maturity and ecosystem-integration gaps.

## Sources

- `redis-vl-dotnet` local source tree at `/Users/steve.lorello/projects/redis/redis-vl-dotnet/src`, version 0.0.4.
- `redis/redis-vl-java` @ `main` (v0.13.1), inventoried from the public source tree on 2026-06-08.
