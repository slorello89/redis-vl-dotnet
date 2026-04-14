# redis-vl-dotnet Feature Analysis

Date: 2026-04-13

## Scope and Method

This analysis compares the current `redis-vl-dotnet` codebase against:

- the local `prd.json` in this repository
- the local `../redis-vl-python` repository
- the upstream `redis-vl-java` GitHub repository at `https://github.com/redis/redis-vl-java`

Important limitation: the Java comparison is based on the public GitHub repository and its published repository files, especially `README.md` and `FEATURE_PARITY_REPORT.md`. I did not run or clone the Java repo locally in this workspace.

## Executive Summary

`redis-vl-dotnet` is materially past the planning stage and already implements most of the core v1 surface described in `prd.json`:

- strongly typed schema definitions
- YAML schema loading
- JSON and HASH document lifecycle
- index lifecycle
- composable filters
- vector, filter, count, hybrid, and vector-range queries
- typed result mapping
- async-first APIs with `CancellationToken`
- embeddings cache
- semantic cache
- semantic router

Based on the current source and tests, the .NET repo looks close to the intended v1 core contract, but it is still behind `redis-vl-python` in breadth. The main gaps versus Python are advanced query surface area, richer schema/index options, AI-adjacent utilities, and developer tooling.

## Evidence Reviewed

Primary .NET sources reviewed:

- `prd.json`
- `docs/v1-architecture.md`
- `src/RedisVlDotNet/Indexes/SearchIndex.cs`
- `src/RedisVlDotNet/Indexes/SearchQueryCommandBuilder.cs`
- `src/RedisVlDotNet/Schema/SearchSchema.cs`
- `src/RedisVlDotNet/Caches/EmbeddingsCache.cs`
- `src/RedisVlDotNet/Caches/SemanticCache.cs`
- `src/RedisVlDotNet/Workflows/SemanticRouter.cs`
- `tests/RedisVlDotNet.Tests/**/*`

Primary Python sources reviewed:

- `../redis-vl-python/README.md`
- `../redis-vl-python/redisvl/index/index.py`
- `../redis-vl-python/redisvl/query/__init__.py`
- `../redis-vl-python/redisvl/schema/schema.py`
- `../redis-vl-python/redisvl/extensions/router/semantic.py`
- `../redis-vl-python/redisvl/extensions/cache/llm/semantic.py`
- `../redis-vl-python/redisvl/extensions/message_history/semantic_history.py`
- selected Python integration/unit tests under `../redis-vl-python/tests`

Primary Java sources reviewed:

- `https://github.com/redis/redis-vl-java`
- the repository `README.md`
- `FEATURE_PARITY_REPORT.md`

Validation run in this repo:

- `dotnet test redis-vl-dotnet.sln --no-restore`
- Result: 74 passed, 14 skipped, 0 failed
- The skipped tests are Redis-backed integration tests, so the code compiles and unit-tests cleanly here, but live Redis flows were not exercised in this run.

## Status Versus `prd.json`

### Areas that appear implemented now

| PRD area | Current status | Notes |
| --- | --- | --- |
| Parity matrix and architecture decisions | Implemented | `docs/v1-architecture.md` exists and maps required/deferred/out-of-scope features. |
| Strongly typed schema model | Implemented | `SearchSchema`, `IndexDefinition`, field definitions, vector enums and validation are present. |
| YAML schema loading | Implemented | `SearchSchema.FromYaml` and `FromYamlFile` exist. |
| Index lifecycle | Implemented | `Create`, `Exists`, `Info`, `Drop` in `SearchIndex`. |
| JSON document lifecycle | Implemented | Single and batch load, fetch by key/id, delete by key/id. |
| HASH document lifecycle | Implemented | Single and batch load, fetch by key/id, delete by key/id. |
| Composable filters | Implemented | Tag, numeric, text, geo, plus `and` / `or` / `not`. |
| Vector query | Implemented | `VectorQuery` plus `SearchIndex.SearchAsync`. |
| Filter query | Implemented | `FilterQuery` plus typed and raw results. |
| Count query | Implemented | `CountQuery` plus `CountAsync`. |
| Hybrid query | Implemented | `HybridQuery` plus query-builder support. |
| Vector range query | Implemented | `VectorRangeQuery` plus query-builder support. |
| Typed mapping and projection | Implemented | `SearchResults`, `SearchResultMapper`, projection tests. |
| Async-first APIs | Implemented | Async methods and cancellation support across index/query/cache/router surface. |
| Embeddings cache | Implemented | `EmbeddingsCache` with TTL and lookup/store behavior. |
| Semantic cache | Implemented | `SemanticCache` creates an index and supports store/check flows. |
| Semantic routing | Implemented | `SemanticRouter` supports route indexing and nearest-route lookup. |

### Areas that look complete enough for the intended v1 core

These look aligned with the repository's own v1 contract:

- core schema and validation
- core index/document lifecycle
- core retrieval primitives
- typed result handling
- provider-agnostic cache and router workflows

### Areas that remain limited relative to the broader PRD baseline

These are the biggest places where the current implementation is narrower than the upstream-inspired vision:

- Index schema only supports a single `Prefix` string, not multiple prefixes.
- Index schema does not expose key separator or stopwords configuration.
- The query surface does not include a dedicated `TextQuery`.
- The query surface does not expose runtime vector parameters like `EF_RUNTIME`.
- There are no aggregation query types.
- There are no pagination helpers beyond `offset` and `limit` on some query types.
- There are no partial document update helpers.
- There is no CLI.

## Feature Comparison: `redis-vl-dotnet` vs `redis-vl-python`

### Feature matrix

| Area | `redis-vl-dotnet` | `redis-vl-python` | Assessment |
| --- | --- | --- | --- |
| Typed schema objects | Yes | Yes | Near parity for the basic field set. |
| YAML schema loading | Yes | Yes | Near parity for baseline YAML workflows. |
| Multiple prefixes | No | Yes | Python ahead. |
| Key separator config | No | Yes | Python ahead. |
| Stopwords config | No | Yes | Python ahead. |
| JSON storage | Yes | Yes | Near parity for basic CRUD/query use. |
| HASH storage | Yes | Yes | Near parity for basic CRUD/query use. |
| Index create/info/exists/drop | Yes | Yes | Near parity for core lifecycle. |
| Vector query | Yes | Yes | Near parity for baseline KNN. |
| Filter query | Yes | Yes | Near parity for baseline metadata search. |
| Count query | Yes | Yes | Near parity. |
| Hybrid query | Yes | Yes | Near parity for basic text+vector search. |
| Vector range query | Yes | Yes | Near parity for threshold retrieval. |
| Text query | No dedicated type | Yes | Python ahead. |
| Aggregation queries | No | Yes | Python ahead. |
| Multi-vector queries | No | Yes | Python ahead. |
| Runtime vector params | No exposed support | Yes | Python ahead. |
| Typed POCO mapping | Yes | Partial / dict-first | .NET has a stronger typed-default story. |
| Async support | Yes, via async methods on `SearchIndex` | Yes, plus `AsyncSearchIndex` | Functional parity, different API shape. |
| Embeddings cache | Yes | Yes | Near parity at a basic level. |
| Semantic cache | Yes | Yes | Both have it, but Python's is richer. |
| Semantic router | Yes | Yes | Both have it, but Python's is richer. |
| Message history / semantic message history | No | Yes | Python ahead. |
| LLM cache integrations | No dedicated feature beyond semantic cache | Yes | Python ahead. |
| Built-in vectorizers | No | Yes | Python far ahead. |
| Built-in rerankers | No | Yes | Python far ahead. |
| CLI | No | Yes | Python ahead. |
| Redis topology helpers | Minimal | Broad | Python ahead on Sentinel, cluster, async cluster concerns. |

### Where .NET is already strong

`redis-vl-dotnet` is in good shape for a focused v1 library if the goal is core Redis Search and vector workflows without provider lock-in:

- The main index abstraction is coherent and practical.
- The CRUD and query APIs are straightforward and test-backed.
- Typed result mapping is better aligned with idiomatic .NET than Python's default dictionary-heavy result model.
- Async and cancellation support are already part of the core design instead of an afterthought.
- The caches and router keep the package provider-agnostic by accepting caller-supplied embeddings.

### Where Python is materially ahead

The Python repo is operating as a broader AI application toolkit, not just a core Redis vector library. The biggest gaps are:

- Query breadth: `TextQuery`, aggregation queries, multi-vector queries, richer query tuning.
- Schema/index breadth: multiple prefixes, key separator, stopwords, and more index-level configuration.
- AI utilities: built-in vectorizers across multiple providers.
- Post-retrieval tooling: rerankers.
- Higher-level workflows: semantic message history and richer LLM cache workflows.
- Tooling and runtime support: CLI, broader Redis connection/topology support, and more operational helpers.

## Practical Read of Current .NET Feature Maturity

### Effectively present today

The repo already looks usable for:

- code-first or YAML-defined search schemas
- creating and managing Redis Search indexes
- loading and fetching HASH and JSON documents
- composable metadata filters
- vector, hybrid, filter-only, count, and vector-range retrieval
- typed projection into records or POCOs
- async-first application integration
- exact-match embedding reuse
- simple semantic caching
- simple semantic routing

### Still missing for broader upstream parity

If the goal is parity with the broader Python experience, the next likely gaps to close are:

1. Add richer schema/index options:
   multiple prefixes, key separator, stopwords, and any other Redis index tuning that the Java/Python repos already expose.
2. Expand the query model:
   `TextQuery`, aggregation support, runtime vector search params, and possibly multi-vector retrieval.
3. Add higher-level workflows beyond the current router/cache baseline:
   semantic message history and stronger LLM cache semantics.
4. Decide whether the .NET strategy should remain provider-agnostic in core:
   if yes, vectorizers and rerankers likely belong in optional extension packages rather than this base library.
5. Add operational tooling if desired:
   a CLI is absent today.

## Feature Comparison: `redis-vl-dotnet` vs `redis-vl-java`

### What the Java repo currently signals

The current Java repository is materially broader than the .NET implementation here.

From the current Java README, the public Java surface includes:

- schema loading from YAML and maps
- index management
- vector, filter, count, vector-range, hybrid, and aggregate-hybrid queries
- vectorizers
- rerankers
- semantic cache
- semantic router

The Java repository also includes a checked-in `FEATURE_PARITY_REPORT.md` dated 2024-12-10 that claims roughly 95% parity with Python and lists support for:

- multiple prefixes
- stopwords
- `TextQuery`
- `AggregationQuery`
- `MultiVectorQuery`
- runtime vector params like `efRuntime` and `epsilon`
- pagination and batch query helpers
- clear/list/from-existing helpers
- semantic message history
- LangCache-style semantic cache support
- rerankers
- broader connection management including Sentinel and cluster

That parity report is self-reported by the Java repo, so it should be treated as strong repository evidence, but still not as independent verification.

### Direct comparison matrix

| Area | `redis-vl-dotnet` | `redis-vl-java` | Assessment |
| --- | --- | --- | --- |
| Typed schema objects | Yes | Yes | Both have the core model. |
| YAML schema loading | Yes | Yes | Near parity for baseline schema import. |
| Multiple prefixes | No | Yes | Java ahead. |
| Stopwords config | No | Yes | Java ahead. |
| Basic index lifecycle | Yes | Yes | Near parity on create/exists/info/drop. |
| Document load/fetch/delete | Yes | Yes | Near parity for basic CRUD. |
| Clear/list/from-existing helpers | No | Yes | Java ahead. |
| Vector query | Yes | Yes | Near parity for baseline KNN. |
| Filter query | Yes | Yes | Near parity for baseline metadata search. |
| Count query | Yes | Yes | Near parity. |
| Hybrid query | Yes | Yes | Both have it, but Java is richer. |
| Aggregate hybrid query | No | Yes | Java ahead. |
| Text query | No dedicated type | Yes | Java ahead. |
| Aggregation query | No | Yes | Java ahead. |
| Multi-vector query | No | Yes | Java ahead. |
| Runtime vector params | No exposed support | Yes | Java ahead. |
| Pagination helpers | Limited | Yes | Java ahead. |
| Batch query helpers | No | Yes | Java ahead. |
| Typed result mapping | Yes | Mixed / map-oriented | .NET has the stronger typed-default story. |
| Async-first APIs | Yes | No direct async parity in README/parity report | .NET ahead on async-first library shape. |
| Embeddings cache | Yes | Yes | Near parity at a basic level. |
| Semantic cache | Yes | Yes | Both have it, Java appears richer. |
| LangCache-style semantic cache | No | Yes | Java ahead. |
| Semantic router | Yes | Yes | Both have it, Java appears richer. |
| Semantic message history | No | Yes | Java ahead. |
| Built-in vectorizers | No | Yes | Java far ahead. |
| Built-in rerankers | No | Yes | Java far ahead. |
| CLI | No | No | Neither repo appears to ship a CLI. |
| Connection/topology breadth | Minimal | Yes | Java ahead. |

### Where .NET is behind Java

This is the clearest conclusion from the direct Java comparison: `redis-vl-dotnet` is much closer to a focused core-v1 library, while `redis-vl-java` is already a broader platform.

The biggest Java advantages are:

- richer schema and index configuration
- broader query surface
- runtime vector tuning
- pagination and batch helpers
- built-in vectorizers and rerankers
- message history workflows
- richer cache ecosystem
- broader Redis connection support

### Where .NET still has a meaningful advantage

The most obvious area where the current .NET implementation is stronger by design is API shape for .NET consumers:

- async-first surface with `CancellationToken`
- typed mapping into records/POCOs as a first-class default

That is a real product distinction, but it does not offset the breadth gap with Java.

## Bottom Line

`redis-vl-dotnet` is no longer just a PRD-driven scaffold. It already implements most of the v1 core described in this repo and is plausibly close to a focused "core Redis vector library" release.

Relative to `redis-vl-python`, the .NET library is strongest in typed .NET ergonomics and is weakest in breadth. Python is still the larger platform: more schema/index options, more query types, more AI utilities, more workflow primitives, and more tooling.

Relative to `redis-vl-java`, the current .NET repo is not at parity. It covers much of the core v1 contract selected in this repository, but Java is clearly ahead on breadth and maturity based on its current public repo surface.

## Sources

- Java repo: `https://github.com/redis/redis-vl-java`
- Java README: `https://github.com/redis/redis-vl-java/blob/main/README.md`
- Java parity report: `https://github.com/redis/redis-vl-java/blob/main/FEATURE_PARITY_REPORT.md`
