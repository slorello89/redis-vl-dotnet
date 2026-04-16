# RedisVL .NET Parity Roadmap

Date: 2026-04-13

This document replaces the completed v1 architecture plan as the active roadmap contract for `redis-vl-dotnet`.
It uses the current repository implementation as the baseline, treats `redis-vl-java` as the primary cross-language parity target, and uses `redis-vl-python` to confirm broader RedisVL workflows worth adopting in .NET.
As of 2026-04-13, the core parity wave is largely implemented and this file should be read as a status snapshot plus remaining-gap tracker rather than a greenfield build plan.

## Roadmap Labels

- `Required`: in scope for the current parity roadmap and tracked by `prd.json`
- `Deferred`: valuable parity work, but not part of the current committed implementation wave
- `Out of scope`: intentionally excluded from the core package even if another language library supports it

## Capability Matrix

| Capability area | .NET now | Java | Python | Roadmap status | Notes |
| --- | --- | --- | --- | --- | --- |
| Core schema model | Yes | Yes | Yes | Implemented | Strong parity for the baseline field model. |
| YAML schema loading | Yes | Yes | Yes | Implemented | Current YAML support covers the baseline schema flow. |
| Multiple prefixes | Yes | Yes | Yes | Implemented | Supported in schema, commands, and integration coverage. |
| Key separator config | Yes | Partial / implied | Yes | Implemented | Preserved in local schema/YAML metadata; Redis Search does not round-trip it as FT.CREATE state. |
| Stopwords config | Yes | Yes | Yes | Implemented | Supports default, disabled, and custom stopword modes. |
| Remaining advanced field and index options | Yes | Yes | Yes | Implemented | Covered where Redis Search exposes a stable typed surface. |
| Advanced YAML schema options | Yes | Yes | Yes | Implemented | Advanced schema options load into the typed model. |
| Index create / exists / info / drop | Yes | Yes | Yes | Implemented | Core lifecycle is already present. |
| Index clear helper | Yes | Yes | Yes | Implemented | Present and integration-tested. |
| Index listing helper | Yes | Yes | Yes | Implemented | Present and integration-tested. |
| Construct from existing index | Yes | Yes | Yes | Implemented | Reconnect flow is implemented; some metadata is limited by Redis FT.INFO. |
| JSON partial updates | Yes | Limited | Yes | Implemented | Present and test-backed. |
| HASH partial updates | Yes | Limited | Yes | Implemented | Present and test-backed. |
| Filter query | Yes | Yes | Yes | Implemented | Baseline metadata search parity is already present. |
| Vector query | Yes | Yes | Yes | Implemented | Baseline KNN flow is already present. |
| Hybrid query | Yes | Yes | Yes | Implemented | Current support covers the basic search flow. |
| Vector range query | Yes | Yes | Yes | Implemented | Present in current command builder and tests. |
| Count query | Yes | Yes | Yes | Implemented | Present and test-backed. |
| Dedicated text query | Yes | Yes | Yes | Implemented | Present in the query model and integration tests. |
| Aggregation query | Yes | Yes | Yes | Implemented | Present with grouping, reducers, paging, and typed mapping. |
| Aggregation + hybrid query | Yes | Yes | Partial | Implemented | Implemented where Redis Search supports the combination cleanly. |
| Multi-vector query | Yes | Yes | Yes | Implemented | Present in the command builder and integration coverage. |
| Runtime vector params | Yes | Yes | Yes | Implemented | Includes practical runtime tuning knobs. |
| Pagination / batch query helpers | Yes | Yes | Yes | Implemented | Query pagination and deterministic batch iteration are present. |
| Typed result mapping | Yes | Mixed | Partial | Implemented | This is already a .NET strength and should be preserved. |
| Async-first index/query APIs | Yes | Limited | Separate async client | Implemented | Intentional .NET-native difference, not a parity gap. |
| Embeddings cache | Yes | Yes | Yes | Implemented | Baseline feature is already present. |
| Semantic cache | Yes | Yes | Yes | Implemented | Keep provider-agnostic in the core package. |
| Semantic router | Yes | Yes | Yes | Implemented | Baseline feature is already present. |
| Message history / semantic message history | Yes | Yes | Yes | Implemented | Present as workflow helpers with unit and integration coverage. |
| Built-in vectorizers | Yes via extensions | Yes | Yes | Implemented | Shipped as optional extension packages, not in the core assembly. |
| Built-in rerankers | Yes via extensions | Yes | Yes | Implemented | Shipped as optional extension packages, not in the core assembly. |
| LangCache-style integrations | No | Yes | Yes | Deferred | Still a useful follow-on integration layer. |
| Redis topology breadth (Sentinel / cluster helpers) | Yes | Yes | Yes | Implemented | Cluster and Sentinel connection helpers and tests are present. |
| CLI | Yes | No repo CLI found | Yes | Implemented | Schema and index workflows are available through `RedisVL.Cli`. |

## Intentional .NET-Native Differences

The roadmap targets feature parity, not API cloning. These differences are intentional and should remain even after parity work lands:

- Async methods with `CancellationToken` stay as the primary .NET API shape instead of introducing a separate async client type.
- Typed mapping to records and POCOs remains the default result-handling workflow instead of a dictionary-first model.
- Provider-specific AI dependencies such as vectorizers and rerankers stay out of the core package unless they can be added as optional extensions.
- Query helpers should map upstream Redis Search capabilities, but the public API should stay idiomatic to .NET naming and construction patterns.

## Delivery Order

Most items from the original `prd.json` delivery order are now complete. The remaining roadmap focus is narrower:

1. Keep CI and integration coverage aligned with the Redis Stack versions used in automation
2. Close any remaining ecosystem-level gaps such as LangCache-style integrations
3. Refine documentation, examples, and packaging now that the parity surface is broad
4. Preserve .NET-native ergonomics while validating parity against upstream RedisVL changes over time

## Source Inputs

- Local implementation and tests in this repository
- [docs/feature-analysis.md](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/feature-analysis.md)
- `redis-vl-java` public repository and `FEATURE_PARITY_REPORT.md`
- Local sibling `../redis-vl-python` repository
