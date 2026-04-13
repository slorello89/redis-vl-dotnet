# RedisVL .NET Parity Roadmap

Date: 2026-04-13

This document replaces the completed v1 architecture plan as the active roadmap contract for `redis-vl-dotnet`.
It uses the current repository implementation as the baseline, treats `redis-vl-java` as the primary cross-language parity target, and uses `redis-vl-python` to confirm broader RedisVL workflows worth adopting in .NET.

## Roadmap Labels

- `Required`: in scope for the current parity roadmap and tracked by `prd.json`
- `Deferred`: valuable parity work, but not part of the current committed implementation wave
- `Out of scope`: intentionally excluded from the core package even if another language library supports it

## Capability Matrix

| Capability area | .NET now | Java | Python | Roadmap status | Notes |
| --- | --- | --- | --- | --- | --- |
| Core schema model | Yes | Yes | Yes | Implemented | Strong parity for the baseline field model. |
| YAML schema loading | Yes | Yes | Yes | Implemented | Current YAML support covers the baseline schema flow. |
| Multiple prefixes | No | Yes | Yes | Required | Needed for upstream schema parity. |
| Key separator config | No | Partial / implied | Yes | Required | Needed to match advanced upstream index metadata. |
| Stopwords config | No | Yes | Yes | Required | Includes default, disabled, and custom stopword modes. |
| Remaining advanced field and index options | Partial | Yes | Yes | Required | Track only options that map cleanly to Redis Search and a typed .NET API. |
| Advanced YAML schema options | No | Yes | Yes | Required | YAML must load the same advanced schema surface supported in code-first APIs. |
| Index create / exists / info / drop | Yes | Yes | Yes | Implemented | Core lifecycle is already present. |
| Index clear helper | No | Yes | Yes | Required | Needed to preserve index definitions while deleting indexed data. |
| Index listing helper | No | Yes | Yes | Required | Needed for inspection and tooling workflows. |
| Construct from existing index | No | Yes | Yes | Required | Needed to reconnect to indexes created elsewhere. |
| JSON partial updates | No | Limited | Yes | Required | Needed for practical JSON lifecycle parity. |
| HASH partial updates | No | Limited | Yes | Required | Needed for HASH lifecycle parity. |
| Filter query | Yes | Yes | Yes | Implemented | Baseline metadata search parity is already present. |
| Vector query | Yes | Yes | Yes | Implemented | Baseline KNN flow is already present. |
| Hybrid query | Yes | Yes | Yes | Implemented | Current support covers the basic search flow. |
| Vector range query | Yes | Yes | Yes | Implemented | Present in current command builder and tests. |
| Count query | Yes | Yes | Yes | Implemented | Present and test-backed. |
| Dedicated text query | No | Yes | Yes | Required | Needed so full-text search is not overloaded onto filter semantics. |
| Aggregation query | No | Yes | Yes | Required | Needed for grouping and reducer workflows. |
| Aggregation + hybrid query | No | Yes | Partial | Required | Included because Java exposes it and it fits Redis Search capabilities. |
| Multi-vector query | No | Yes | Yes | Deferred | Valuable, but not required for the first parity wave. |
| Runtime vector params | No | Yes | Yes | Required | Include the practical tuning knobs already surfaced upstream. |
| Pagination / batch query helpers | Partial | Yes | Yes | Required | Should stay idiomatic to .NET while covering common upstream workflows. |
| Typed result mapping | Yes | Mixed | Partial | Implemented | This is already a .NET strength and should be preserved. |
| Async-first index/query APIs | Yes | Limited | Separate async client | Implemented | Intentional .NET-native difference, not a parity gap. |
| Embeddings cache | Yes | Yes | Yes | Implemented | Baseline feature is already present. |
| Semantic cache | Yes | Yes | Yes | Implemented | Keep provider-agnostic in the core package. |
| Semantic router | Yes | Yes | Yes | Implemented | Baseline feature is already present. |
| Message history / semantic message history | No | Yes | Yes | Required | Needed for broader workflow parity. |
| Built-in vectorizers | No | Yes | Yes | Out of scope | Keep vendor SDK dependencies out of the core package. |
| Built-in rerankers | No | Yes | Yes | Out of scope | Same rationale as vectorizers. |
| LangCache-style integrations | No | Yes | Yes | Deferred | Useful, but not required for the core parity contract. |
| Redis topology breadth (Sentinel / cluster helpers) | Minimal | Yes | Yes | Deferred | Important, but secondary to schema/query parity. |
| CLI | No | No repo CLI found | Yes | Deferred | Worth adding, but not required before core library parity closes. |

## Intentional .NET-Native Differences

The roadmap targets feature parity, not API cloning. These differences are intentional and should remain even after parity work lands:

- Async methods with `CancellationToken` stay as the primary .NET API shape instead of introducing a separate async client type.
- Typed mapping to records and POCOs remains the default result-handling workflow instead of a dictionary-first model.
- Provider-specific AI dependencies such as vectorizers and rerankers stay out of the core package unless they can be added as optional extensions.
- Query helpers should map upstream Redis Search capabilities, but the public API should stay idiomatic to .NET naming and construction patterns.

## Delivery Order

The current `prd.json` priority order is still the implementation order:

1. Schema parity gaps: multiple prefixes, key separator, stopwords, and remaining advanced field/index options
2. YAML parity for advanced schema definitions
3. Index lifecycle helpers: clear, list, and from-existing
4. Partial update helpers for JSON and HASH storage
5. Query-model expansion: text query, aggregation, hybrid aggregation, runtime vector parameters, multi-vector where selected
6. Higher-level workflow parity such as message history
7. Deferred operational/tooling work such as broader connection helpers and CLI support

## Source Inputs

- Local implementation and tests in this repository
- [docs/feature-analysis.md](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/feature-analysis.md)
- `redis-vl-java` public repository and `FEATURE_PARITY_REPORT.md`
- Local sibling `../redis-vl-python` repository
