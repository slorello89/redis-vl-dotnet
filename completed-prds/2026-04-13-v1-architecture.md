# redis-vl-dotnet v1 Architecture and Parity Matrix

## Purpose

This document defines the v1 contract for `redis-vl-dotnet`. It uses `redis-vl-java` as the primary parity baseline for a .NET implementation and selectively adopts workflows from `redis-vl-python` where they fit .NET conventions and keep the core package provider-agnostic.

Status labels used in the parity matrix:

- `Required`: in scope for the first usable release
- `Deferred`: intentionally postponed until after v1
- `Out of scope`: not planned for the core v1 package

## Architecture Decisions

## ADR-001: Central abstraction is a typed search index

The library will revolve around a `SearchIndex`-style abstraction that owns:

- schema metadata
- index lifecycle operations
- document lifecycle operations
- query execution

Rationale: both upstream libraries center operational workflows on an index abstraction, and it provides a single composition point for Redis connection handling, schema validation, and typed result mapping.

## ADR-002: Schema is strongly typed first, file-backed second

V1 will define strongly typed schema objects for index metadata and field definitions. File-based schema loading will map into those same types rather than creating a parallel configuration model.

Rationale: .NET consumers expect compile-time guidance through enums, records, and option objects. A single schema model also prevents drift between code-first and file-based flows.

## ADR-003: YAML is the v1 file format

V1 will support YAML schema loading for parity with both upstream libraries. YAML files will be deserialized into the same strongly typed schema model used by code-first definitions.

Rationale: YAML is the established upstream interchange format and is the shortest path to practical workflow parity.

## ADR-004: Both JSON and HASH storage modes are first-class

The public surface must support JSON and HASH storage through one consistent index abstraction. Storage-specific behavior may vary internally, but API naming and lifecycle semantics should remain aligned.

Rationale: the upstream contract treats storage mode as a schema concern, not as two separate client stacks.

## ADR-005: Query objects are first-class, not raw query strings

V1 query flows will be modeled as explicit query types:

- `VectorQuery`
- `FilterQuery`
- `CountQuery`
- `HybridQuery`
- `VectorRangeQuery`

Filter expressions will also be strongly typed and composable.

Rationale: .NET developers should not have to assemble RediSearch syntax by hand for standard workflows.

## ADR-006: Provider-specific AI integrations stay out of the core package

The core package will accept caller-supplied embeddings and provider-agnostic extension interfaces. Built-in OpenAI or cloud-vendor vectorizer dependencies are not part of v1.

Rationale: this keeps the package small, avoids SDK churn, and fits .NET expectations around dependency boundaries.

## ADR-007: Async-first public APIs

The long-term public contract is async-first for Redis-backed operations, with `CancellationToken` support where practical. Sync convenience APIs may exist later, but async behavior drives the design.

Rationale: network-bound .NET libraries integrate most cleanly with ASP.NET, worker services, and modern hosting models when the async surface is primary.

## ADR-008: Typed mapping is preferred over dictionary-first results

Raw result access may be preserved for escape hatches, but the default direction for v1 is predictable mapping into typed .NET shapes with explicit projection and alias handling.

Rationale: .NET consumers generally expect serializer-driven materialization rather than pervasive `Dictionary<string, object>` handling.

## V1 Parity Matrix

| Area | Capability | redis-vl-java | redis-vl-python | redis-vl-dotnet v1 | Notes |
| --- | --- | --- | --- | --- | --- |
| Schema | Index metadata: name, prefix, storage type | Yes | Yes | Required | Must be strongly typed in .NET |
| Schema | Text, tag, numeric, geo, and vector fields | Yes | Yes | Required | Core field set for v1 |
| Schema | Vector validation for dims, datatype, algorithm, distance metric | Yes | Yes | Required | Fail before sending Redis commands |
| Schema | Multiple prefixes and index-level options | Yes | Yes | Deferred | Useful, but not required for initial contract |
| Schema | Stopwords and advanced index tuning | Yes | Yes | Deferred | Keep initial schema surface narrow |
| Schema | Schema loading from YAML | Yes | Yes | Required | Shared model with code-first schema |
| Schema | Alternate file formats beyond YAML | Partial | Partial | Out of scope | Keep one interchange format in v1 |
| Index lifecycle | Create index | Yes | Yes | Required | Include overwrite or safe-create behavior |
| Index lifecycle | Exists and info inspection | Yes | Yes | Required | Needed for setup and diagnostics |
| Index lifecycle | Drop or delete index | Yes | Yes | Required | Cleanup path is required |
| Index lifecycle | Clear or enumerate all indexes | Yes | Yes | Deferred | Nice to have, not core to first release |
| Document lifecycle | Load single document | Yes | Yes | Required | JSON and HASH modes |
| Document lifecycle | Batch load | Yes | Yes | Required | Needed for realistic ingestion |
| Document lifecycle | Fetch by id or key | Yes | Yes | Required | Consistent across storage modes |
| Document lifecycle | Delete by id or key | Yes | Yes | Required | Consistent across storage modes |
| Document lifecycle | Partial update helpers | Yes | Partial | Deferred | Can be added after core round-trip support |
| Query types | Vector query | Yes | Yes | Required | Foundational semantic retrieval primitive |
| Query types | Filter query | Yes | Yes | Required | Metadata-only retrieval |
| Query types | Count query | Yes | Yes | Required | Required for pagination and analytics workflows |
| Query types | Hybrid query | Yes | Yes | Required | Core parity target |
| Query types | Vector range query | Yes | Yes | Required | Needed for threshold retrieval workflows |
| Query types | Text query | Yes | Yes | Deferred | Can be layered on the filter stack later |
| Query features | Return field projection and aliases | Yes | Yes | Required | Needed for typed mapping |
| Query features | Metadata pre-filters | Yes | Yes | Required | Included in vector and hybrid flows |
| Query features | Runtime vector search params | Yes | Yes | Deferred | Add after basic vector querying is stable |
| Query features | Pagination and batch query helpers | Yes | Yes | Deferred | Valuable, but not needed for first usable API |
| Filters | Tag predicates | Yes | Yes | Required | |
| Filters | Numeric predicates | Yes | Yes | Required | |
| Filters | Text predicates | Yes | Yes | Required | |
| Filters | Geo predicates | Yes | Yes | Required | |
| Filters | Logical composition: and, or, not | Yes | Yes | Required | |
| Results | Raw Redis result access | Yes | Yes | Required | Escape hatch for advanced scenarios |
| Results | Typed mapping into POCOs or records | Yes | Partial | Required | .NET-native default direction |
| Results | Nested mapping and vector field handling | Yes | Yes | Required | Must surface clear mapping failures |
| Async model | Async index and query APIs | No direct parity | Yes | Required | .NET-specific contract |
| Caches | Embeddings cache | Yes | Yes | Required | Provider-agnostic embedding reuse |
| Caches | Semantic cache | Yes | Yes | Required | Caller-supplied embeddings or interface |
| Caches | Cache models and advanced cache helpers | Yes | Yes | Deferred | Minimal usable cache API first |
| Higher-level workflows | Semantic routing | Yes | Yes | Required | Selected v1 workflow primitive for provider-agnostic intent routing |
| Higher-level workflows | Message history or semantic message history | Yes | Yes | Deferred | Useful, but not necessary for base library release |
| Higher-level workflows | Rerankers and vectorizer integrations | Yes | Yes | Out of scope | Keep core package provider-agnostic |
| Tooling | CLI for index management | Partial | Yes | Out of scope | Prefer library-first delivery before tooling |

## Intentional .NET-Native Divergences

## Public API shape

- Prefer records, enums, and option objects over stringly typed maps.
- Favor method overloads or option types that map cleanly to C# call sites instead of builder-heavy Java patterns.
- Use `CancellationToken` on network-bound async APIs.

## Serialization and mapping

- Default document serialization should align with `System.Text.Json` conventions and allow pluggable serializer behavior later.
- Typed result mapping is a first-class scenario, not an add-on after raw dictionary access.

## Dependency boundaries

- The core package should not depend on vendor-specific embedding SDKs.
- AI-provider integrations, if added later, should live in optional extension packages.

## Scope boundaries for v1

- No CLI in the first release.
- No built-in reranker or hosted vectorizer implementations in the core package.
- Higher-level workflows are additive only after schema, lifecycle, query, and cache primitives are stable.
- V1 includes semantic routing only; message-history workflows remain deferred until the base storage and retrieval surface sees broader usage.

## Delivery Sequence

The execution order in `prd.json` matches the intended architecture layering:

1. schema contracts
2. validation and file loading
3. index and document lifecycle
4. query primitives and typed result mapping
5. async surface
6. caches
7. selected higher-level workflows
8. deterministic integration harness and CI
