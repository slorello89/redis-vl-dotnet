# PRD: RedisVL .NET Full Parity Roadmap

## Introduction

`redis-vl-dotnet` already covers the core v1 library surface, but it remains behind both `redis-vl-java` and `redis-vl-python` in breadth. This PRD defines an aggressive parity roadmap to close the currently identified gaps across schema configuration, index lifecycle helpers, advanced queries, higher-level AI workflows, provider integrations, connection support, and developer tooling.

The goal is not to redesign the existing library from scratch. The goal is to extend the current implementation in a way that preserves .NET-native ergonomics while reaching practical feature parity with the broader RedisVL ecosystem.

Every missing feature in scope must include:

- implementation
- unit and integration tests
- at least one runnable example or documented sample

## Goals

- Close the highest-value feature gaps against `redis-vl-java` and `redis-vl-python`.
- Preserve the existing async-first, strongly typed .NET API direction.
- Ensure every new feature ships with deterministic tests and a runnable example.
- Make parity progress measurable by replacing the old v1 plan with a gap-closure roadmap.
- Keep provider-specific AI integrations isolated so the base library remains maintainable.

## User Stories

### US-001: Re-establish the parity contract
**Description:** As a maintainer, I want a new parity roadmap document so that implementation work tracks current Java and Python gaps instead of the already-completed v1 baseline.

**Acceptance Criteria:**
- [ ] Add a new parity matrix in repo docs that compares current `.NET`, Java, and Python capabilities
- [ ] Mark each gap as required for this roadmap, deferred, or out of scope
- [ ] Document any intentional .NET-native API differences that remain after parity work
- [ ] Typecheck passes

### US-002: Add richer index metadata options
**Description:** As a .NET developer, I want index metadata to support multiple prefixes, key separators, and stopwords so that schemas match upstream RedisVL capabilities.

**Acceptance Criteria:**
- [ ] Extend schema metadata to support multiple prefixes
- [ ] Add key separator configuration
- [ ] Add stopwords configuration including Redis default, explicit empty list, and custom list behavior
- [ ] Add unit tests for schema construction and validation
- [ ] Add integration tests proving generated `FT.CREATE` arguments are accepted by Redis
- [ ] Add or update an example showing advanced schema metadata configuration
- [ ] Typecheck passes

### US-003: Expand field and schema parity options
**Description:** As a .NET developer, I want the schema model to expose missing upstream field and index options so that YAML and code-first schemas can represent advanced Redis search definitions.

**Acceptance Criteria:**
- [ ] Identify remaining field-level schema gaps from Java and Python and implement the selected options
- [ ] Preserve strong typing with enums, records, and option objects instead of raw string maps
- [ ] Add unit tests for valid and invalid schema combinations
- [ ] Add integration tests covering index creation for the newly supported options
- [ ] Add or update an example schema that uses the added options
- [ ] Typecheck passes

### US-004: Add schema import parity for advanced YAML definitions
**Description:** As a .NET developer, I want YAML schema loading to support the newly added schema options so that upstream-compatible schema files round-trip into the .NET model.

**Acceptance Criteria:**
- [ ] Extend YAML parsing to support new index metadata and field options added in this roadmap
- [ ] Reject unsupported or invalid YAML with clear exceptions
- [ ] Add unit tests for successful parsing and validation failures
- [ ] Add fixture YAML files covering advanced schema scenarios
- [ ] Add an example that loads an advanced YAML schema from disk
- [ ] Typecheck passes

### US-005: Add index discovery and reuse helpers
**Description:** As a .NET developer, I want helper APIs such as clear, list, and load-from-existing behaviors so that I can manage indexes more like the Java and Python libraries.

**Acceptance Criteria:**
- [ ] Add API support for clearing indexed documents without dropping the index
- [ ] Add API support for listing or enumerating indexes
- [ ] Add API support for constructing an index abstraction from an existing Redis index definition
- [ ] Add unit tests for command generation and response parsing
- [ ] Add integration tests for clear, list, and from-existing flows
- [ ] Add an example demonstrating reconnecting to an existing index
- [ ] Typecheck passes

### US-006: Add partial document update support
**Description:** As a .NET developer, I want partial update helpers so that I can modify stored records without replacing the full document payload.

**Acceptance Criteria:**
- [ ] Add partial update APIs for JSON-backed documents
- [ ] Add the supported partial update story for HASH-backed documents and document any storage-specific constraints
- [ ] Define how key/id resolution works for partial updates
- [ ] Add unit tests covering updated fields, missing fields, and invalid update requests
- [ ] Add integration tests for JSON and HASH partial update flows
- [ ] Add an example demonstrating partial updates on an existing record
- [ ] Typecheck passes

### US-007: Add a dedicated TextQuery abstraction
**Description:** As a .NET developer, I want a first-class text query API so that I can perform full-text search without overloading the filter query model.

**Acceptance Criteria:**
- [ ] Add a `TextQuery` type with explicit text-search semantics
- [ ] Support return fields, pagination, and deterministic score behavior
- [ ] Add unit tests for generated RediSearch query syntax and invalid inputs
- [ ] Add integration tests validating result ranking on seeded data
- [ ] Add an example demonstrating full-text search
- [ ] Typecheck passes

### US-008: Add aggregation query support
**Description:** As a .NET developer, I want aggregation query APIs so that I can perform grouping, reducers, and analytics workflows supported by upstream libraries.

**Acceptance Criteria:**
- [ ] Add a first-class aggregation query abstraction
- [ ] Support core aggregate pipeline operations selected for parity
- [ ] Add raw and typed result handling for aggregation responses
- [ ] Add unit tests for command generation and result parsing
- [ ] Add integration tests for grouping and reducer scenarios on seeded data
- [ ] Add an example demonstrating an aggregation workflow
- [ ] Typecheck passes

### US-009: Add aggregate hybrid query support
**Description:** As a .NET developer, I want aggregate hybrid query support so that I can combine semantic retrieval with aggregation workflows like the Java library.

**Acceptance Criteria:**
- [ ] Add an aggregate-hybrid query abstraction that combines vector search and aggregate processing
- [ ] Define supported filters, return fields, and aggregation stages
- [ ] Add unit tests for command generation and unsupported combinations
- [ ] Add integration tests for deterministic aggregate-hybrid scenarios
- [ ] Add an example demonstrating aggregate-hybrid search
- [ ] Typecheck passes

### US-010: Add multi-vector query support
**Description:** As a .NET developer, I want multi-vector query support so that I can query across multiple vectors or vector clauses when upstream workflows require it.

**Acceptance Criteria:**
- [ ] Add a multi-vector query abstraction with explicit field and vector inputs
- [ ] Define scoring and ordering semantics for multi-vector search
- [ ] Add unit tests for argument building and validation
- [ ] Add integration tests for deterministic multi-vector retrieval behavior
- [ ] Add an example demonstrating multi-vector search
- [ ] Typecheck passes

### US-011: Add runtime vector search tuning parameters
**Description:** As a .NET developer, I want query-time vector tuning options such as `EF_RUNTIME` and `EPSILON` so that I can tune performance and recall at runtime.

**Acceptance Criteria:**
- [ ] Add strongly typed runtime vector search parameter options to the relevant query types
- [ ] Reject invalid parameter combinations for unsupported vector algorithms
- [ ] Add unit tests for parameter validation and command generation
- [ ] Add integration tests showing the parameters are accepted in supported scenarios
- [ ] Add an example demonstrating runtime vector tuning
- [ ] Typecheck passes

### US-012: Add pagination and batch query helpers
**Description:** As a .NET developer, I want consistent pagination and batch query helpers so that I can work with large result sets without reimplementing common iteration logic.

**Acceptance Criteria:**
- [ ] Add shared pagination support across all relevant query types
- [ ] Add batch query helper APIs or iterators for repeated retrieval workflows
- [ ] Define deterministic continuation behavior for callers
- [ ] Add unit tests for pagination and batching edge cases
- [ ] Add integration tests for multi-page retrieval flows
- [ ] Add an example demonstrating paged iteration
- [ ] Typecheck passes

### US-013: Add semantic message history workflows
**Description:** As a .NET developer, I want semantic message history primitives so that I can store and retrieve conversational context for AI applications.

**Acceptance Criteria:**
- [ ] Add message history and semantic message history workflows selected for parity
- [ ] Support storing role, content, metadata, timestamps, and session identity
- [ ] Support recent-message retrieval and semantic retrieval with configurable thresholds
- [ ] Add unit tests for schema, validation, and retrieval logic
- [ ] Add integration tests for recent and semantic history flows
- [ ] Add an example demonstrating conversational context retrieval
- [ ] Typecheck passes

### US-014: Add richer semantic cache workflows
**Description:** As a .NET developer, I want the semantic cache to support richer upstream-style cache records and filters so that it covers more real LLM caching scenarios.

**Acceptance Criteria:**
- [ ] Extend semantic cache entries to support metadata and structured cache payloads
- [ ] Support filterable cache retrieval where the design allows it
- [ ] Define overwrite, TTL, and schema compatibility behavior clearly
- [ ] Add unit tests for metadata handling, filters, and validation
- [ ] Add integration tests for semantic cache hit, miss, threshold, TTL, and filter scenarios
- [ ] Add an example demonstrating enriched semantic cache usage
- [ ] Typecheck passes

### US-015: Add built-in vectorizer extension packages
**Description:** As a .NET developer, I want optional vectorizer integrations so that I can generate embeddings without building adapter code for every provider myself.

**Acceptance Criteria:**
- [ ] Add optional extension packages for the selected providers rather than placing provider SDKs in the core package
- [ ] Define a stable vectorizer abstraction shared across extension packages
- [ ] Add unit tests for provider abstraction behavior and serialization contracts
- [ ] Add integration or smoke-test coverage for each supported provider where practical
- [ ] Add at least one example per supported provider package
- [ ] Typecheck passes

### US-016: Add reranker extension packages
**Description:** As a .NET developer, I want optional reranker integrations so that I can improve retrieval relevance using upstream-style reranking workflows.

**Acceptance Criteria:**
- [ ] Add a provider-agnostic reranker abstraction
- [ ] Add optional extension packages for the selected reranker providers
- [ ] Add unit tests for ranking contracts and error handling
- [ ] Add integration or smoke-test coverage for supported reranker packages where practical
- [ ] Add an example demonstrating search plus reranking
- [ ] Typecheck passes

### US-017: Add broader Redis connection and topology support
**Description:** As a .NET developer, I want first-class support for common Redis deployment topologies so that the library works in more production environments.

**Acceptance Criteria:**
- [ ] Document and implement the selected topology support for standalone, cluster, and Sentinel-style deployments where supported by the .NET Redis client
- [ ] Add connection factory or configuration helpers where needed
- [ ] Add unit tests for connection configuration parsing and validation
- [ ] Add integration coverage for each supported topology where practical in CI or gated local test suites
- [ ] Add examples for the supported connection modes
- [ ] Typecheck passes

### US-018: Add a CLI for index and schema workflows
**Description:** As a maintainer or developer, I want a CLI for common index and schema operations so that I can inspect and manage RedisVL artifacts from the terminal.

**Acceptance Criteria:**
- [ ] Add a CLI project with commands for schema load, index create, info, list, clear, and delete
- [ ] Define a stable command-line interface and help output
- [ ] Add unit tests for command parsing and output behavior
- [ ] Add integration tests for at least the core create, info, list, and delete flows
- [ ] Add a CLI example section to repo docs
- [ ] Typecheck passes

### US-019: Expand examples and documentation coverage
**Description:** As a maintainer, I want every newly added feature area documented with examples so that users can discover and adopt the expanded parity surface.

**Acceptance Criteria:**
- [ ] Add or update examples for every feature area introduced in this roadmap
- [ ] Add a docs index that maps features to examples and API entry points
- [ ] Document environment prerequisites for provider-specific packages and integration tests
- [ ] Verify all example projects build successfully
- [ ] Typecheck passes

### US-020: Strengthen the test harness for the expanded parity surface
**Description:** As a maintainer, I want deterministic test coverage for every new parity feature so that the broader surface remains safe to evolve.

**Acceptance Criteria:**
- [ ] Add unit tests for all new schema, lifecycle, query, workflow, provider, and CLI features
- [ ] Add seeded Redis-backed integration tests for all new runtime behaviors
- [ ] Separate provider-gated tests from core tests so the default repo test run stays reliable
- [ ] Document the test matrix and how to run each tier locally and in CI
- [ ] `dotnet test` passes for the default test suite

## Functional Requirements

- FR-1: The system must support index metadata parity features including multiple prefixes, key separator configuration, and stopwords configuration.
- FR-2: The system must support advanced schema options in both code-first and YAML-loaded schemas.
- FR-3: The system must support clear, list, and from-existing index lifecycle helpers.
- FR-4: The system must support partial updates for the selected storage modes with explicit, documented behavior.
- FR-5: The system must add a dedicated `TextQuery` abstraction.
- FR-6: The system must add aggregation query support.
- FR-7: The system must add aggregate-hybrid query support.
- FR-8: The system must add multi-vector query support.
- FR-9: The system must support runtime vector search parameters on the relevant query types.
- FR-10: The system must provide consistent pagination and batch query helpers.
- FR-11: The system must provide message history and semantic message history workflows.
- FR-12: The system must expand semantic cache capabilities to cover richer cache records and filterable retrieval workflows.
- FR-13: The system must support optional vectorizer packages without introducing provider SDKs into the core package.
- FR-14: The system must support optional reranker packages without introducing provider SDKs into the core package.
- FR-15: The system must support the selected Redis deployment topologies supported by the underlying .NET Redis client.
- FR-16: The system must ship a CLI for common schema and index management workflows.
- FR-17: Every feature added in this roadmap must include unit tests, integration tests where applicable, and at least one example.
- FR-18: Documentation must clearly distinguish core-package features from optional extension-package features.

## Non-Goals

- No commitment to implement every provider available in Java or Python in the first parity wave.
- No requirement to place provider SDK dependencies into the core `RedisVL` package.
- No requirement to reproduce Python or Java APIs verbatim when a more idiomatic .NET shape is better.
- No requirement to add UI or browser-based tooling.
- No requirement to guarantee identical performance characteristics across all languages.

## Technical Considerations

- Preserve the existing async-first surface and `CancellationToken` support.
- Keep strongly typed schema and query models as the default design direction.
- Prefer additive changes that do not break the current core API surface.
- Separate core features from provider-specific extension packages at the solution and package level.
- Reuse the existing deterministic integration-test style with seeded Redis fixtures.
- Treat Java and Python parity as feature-parity targets, not API-shape cloning targets.

## Success Metrics

- The repo closes the currently identified feature gaps called out in `docs/feature-analysis.md`.
- Every roadmap feature area has at least one runnable example and test coverage.
- The default `dotnet test` run remains green without requiring external provider credentials.
- The parity matrix can mark the major Java/Python gaps as implemented or intentionally deferred.
- A maintainer can point users to concrete .NET equivalents for the major Java/Python workflows currently missing.

## Open Questions

- Which vectorizer providers should be included in the first extension-package wave?
- Which reranker providers should be included in the first extension-package wave?
- Should semantic message history live in the core package or in a workflows package?
- How much topology coverage is practical to validate in CI versus documentation plus gated integration tests?
- Should the CLI ship in the same repository but a separate package, or remain an internal maintainer tool at first?
