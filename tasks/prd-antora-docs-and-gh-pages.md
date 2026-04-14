# PRD: Antora Documentation and GitHub Pages Setup

## Introduction

`redis-vl-dotnet` already has broad feature coverage, runnable examples, and implementation notes spread across `README.md`, `docs/`, `examples/`, and older planning artifacts. The repository now needs a documentation system that is publishable, structured for growth, and explicit enough that each major feature area can be learned, validated, and maintained without rediscovering behavior from source code or tests.

This PRD defines the work to migrate and expand the current documentation into an Antora-based documentation site published from GitHub Actions to GitHub Pages. The resulting docs must fully cover the current library surface, provide one working example per major feature area, and clearly mark any required credentials as environment variables instead of embedded secrets.

The goal is documentation and documentation infrastructure. This PRD does not change the library feature set itself except where documentation gaps require example cleanup or minimal example completion work.

## Goals

- Replace the current ad hoc docs layout with an Antora site that has a clear information architecture.
- Publish the docs automatically to GitHub Pages from GitHub Actions.
- Ensure every major feature area has a complete reference or guide page plus a working example.
- Keep provider-specific examples runnable without bundling secrets by requiring environment variables such as `OPENAI_API_KEY`, `HF_TOKEN`, and `COHERE_API_KEY`.
- Preserve useful existing content by migrating and expanding current docs instead of rewriting blindly.
- Establish verification gates so docs, examples, and Antora site generation remain healthy over time.

## User Stories

### US-001: Establish Antora site structure
**Description:** As a maintainer, I want a standard Antora project structure so that repository documentation has a stable home and can scale as the library grows.

**Acceptance Criteria:**
- [ ] Add the Antora playbook, site configuration, UI selection, and component version metadata required to build the docs locally and in CI.
- [ ] Create a predictable Antora content structure for at least `getting-started`, `core-features`, `extensions`, `examples`, `cli`, `testing`, and `reference`.
- [ ] Define navigation files that expose the full docs tree without orphaned pages.
- [ ] Document the local docs build command and prerequisites in the repository.
- [ ] Antora site build succeeds locally or in CI with no missing-page errors.

### US-002: Migrate existing docs into Antora
**Description:** As a developer, I want the current docs content migrated into the Antora site so that I can use one documentation system instead of bouncing between disconnected markdown files.

**Acceptance Criteria:**
- [ ] Migrate the current useful content from `README.md`, `docs/getting-started.md`, `docs/extensions.md`, `docs/testing.md`, `docs/parity-roadmap.md`, and example READMEs into Antora pages.
- [ ] Preserve technically accurate existing content while removing duplication where Antora pages become the canonical source.
- [ ] Add redirects, README links, or clear repository pointers so readers can find the Antora site entry point from the repo root.
- [ ] Remove or clearly mark superseded non-Antora docs to prevent two conflicting sources of truth.
- [ ] All migrated pages render correctly in the generated site.

### US-003: Document installation and environment prerequisites
**Description:** As a new user, I want a clear installation and prerequisites guide so that I can run the library and examples without trial-and-error.

**Acceptance Criteria:**
- [ ] Add a getting-started page covering .NET SDK requirements, Redis prerequisites, Redis Stack local startup, and current package/reference setup.
- [ ] Document which features require RediSearch, RedisJSON, vector similarity support, cluster nodes, or Sentinel nodes.
- [ ] Document required environment variables for Redis connectivity and provider integrations.
- [ ] Include copy-paste-ready commands for the default local setup flow.
- [ ] The page links to the relevant example and testing pages.

### US-004: Document core schema and index workflows
**Description:** As a .NET developer, I want complete docs for schema and index management so that I can create, inspect, reuse, and remove indexes confidently.

**Acceptance Criteria:**
- [ ] Add pages for `SearchSchema`, `IndexDefinition`, field definitions, YAML schema loading, advanced schema options, and index lifecycle operations.
- [ ] Document JSON and HASH storage modes, multiple prefixes, key separator behavior, stopwords behavior, and construct-from-existing flows where supported.
- [ ] Include a working example for the major schema/index workflow area, using either the existing JSON example or an improved replacement.
- [ ] Include expected Redis prerequisites and any storage-specific constraints.
- [ ] Add links from schema pages to the CLI and example pages where relevant.

### US-005: Document query and aggregation features
**Description:** As a .NET developer, I want each query type documented with runnable examples so that I can choose the correct API without reading internal command builders.

**Acceptance Criteria:**
- [ ] Add dedicated Antora pages for `FilterQuery`, `TextQuery`, `CountQuery`, `VectorQuery`, `VectorRangeQuery`, `HybridQuery`, `AggregateHybridQuery`, `MultiVectorQuery`, and aggregation workflows.
- [ ] Document pagination, batch helpers, return fields, typed result mapping, and runtime vector options.
- [ ] Ensure each major query feature area is covered by at least one working example project or a clearly identified example section.
- [ ] Include guidance on when JSON versus HASH examples are used and why.
- [ ] Include troubleshooting notes for common query mistakes such as missing vector settings or unsupported Redis modules.

### US-006: Document update and document lifecycle workflows
**Description:** As a .NET developer, I want document lifecycle docs so that I can load, fetch, partially update, clear, and drop indexed documents safely.

**Acceptance Criteria:**
- [ ] Document full document load/fetch/delete flows for JSON and HASH-backed indexes.
- [ ] Document JSON partial updates and HASH partial updates, including limitations and expected behavior.
- [ ] Ensure the major document lifecycle area has a working example that demonstrates create, fetch, update, and cleanup.
- [ ] Call out key resolution behavior and required document shape assumptions.
- [ ] Cross-link lifecycle docs with schema and query docs.

### US-007: Document higher-level workflows
**Description:** As an application developer, I want complete workflow docs for caches, routers, and message history so that I can build AI-adjacent features with the library.

**Acceptance Criteria:**
- [ ] Add pages for `EmbeddingsCache`, `SemanticCache`, `SemanticRouter`, `MessageHistory`, and `SemanticMessageHistory`.
- [ ] Document the role of `ITextVectorizer` and the obsolete embedding-generator shim where it affects workflow usage.
- [ ] Ensure the cache workflow area and the message-history/router workflow area each have a working example.
- [ ] Document thresholds, metadata/filter usage, recency behavior, and any vector-dimension assumptions.
- [ ] Include guidance on using in-process demo vectorizers versus provider-backed vectorizers.

### US-008: Document extension packages and provider credentials
**Description:** As a developer using provider integrations, I want clear extension docs so that I can wire OpenAI, Hugging Face, and Cohere packages correctly without hardcoded secrets.

**Acceptance Criteria:**
- [ ] Add extension docs for the vectorizer abstractions, reranker abstractions, OpenAI vectorizer package, Hugging Face vectorizer package, and Cohere reranker package.
- [ ] Document required environment variables such as `OPENAI_API_KEY`, `HF_TOKEN`, and `COHERE_API_KEY` instead of embedding credentials.
- [ ] Include setup, install/reference steps, minimal usage examples, and cleanup steps for each provider-backed example.
- [ ] Ensure each provider area has a working example project with the API key consumed from environment variables.
- [ ] Document expected skip or failure behavior when provider credentials are missing.

### US-009: Document CLI workflows
**Description:** As an operator or developer, I want CLI docs so that I can inspect and manage indexes from the terminal without reading the source.

**Acceptance Criteria:**
- [ ] Add a CLI section covering install/build, command discovery, index lifecycle commands, and schema validation/show commands.
- [ ] Document required connection arguments and example command invocations.
- [ ] Include a working CLI example flow that can be executed against a local Redis instance.
- [ ] Link CLI docs back to the relevant schema and testing pages.
- [ ] Include troubleshooting for missing Redis modules and connection failures.

### US-010: Publish Antora docs to GitHub Pages
**Description:** As a maintainer, I want the docs built and published automatically so that the canonical documentation is available from GitHub without manual deployment steps.

**Acceptance Criteria:**
- [ ] Add a GitHub Actions workflow that builds the Antora site on the default branch.
- [ ] Configure the workflow to publish the generated site to GitHub Pages using supported GitHub Actions deployment steps.
- [ ] Document any required repository settings such as Pages source and permissions.
- [ ] Ensure failed site builds block publishing.
- [ ] The published site serves the expected Antora landing page after a successful workflow run.

### US-011: Add docs quality gates
**Description:** As a maintainer, I want automated verification for docs and examples so that published content does not drift away from the codebase.

**Acceptance Criteria:**
- [ ] Define a docs validation workflow that builds the Antora site in CI.
- [ ] Define an example validation sweep that compiles each example project.
- [ ] Preserve or extend current test guidance so doc changes link to the correct validation commands.
- [ ] Fail CI when a documented example project no longer builds or when Antora navigation contains broken references.
- [ ] Document the local verification commands for maintainers.

### US-012: Organize planning artifacts and completed PRDs
**Description:** As a maintainer, I want completed planning artifacts moved out of the active task area so that current work is easy to find and older planning files remain available for reference.

**Acceptance Criteria:**
- [ ] Create a `completed-prds/` folder for superseded planning artifacts.
- [ ] Move the current root `prd.json` into `completed-prds/`.
- [ ] Move superseded PRD markdown files into `completed-prds/` while leaving the active PRD in `tasks/`.
- [ ] Keep filenames intact unless a rename is needed to avoid collisions.
- [ ] Add no changes that would hide or delete historical planning context.

## Functional Requirements

1. FR-1: The repository must include a working Antora documentation scaffold with a local build path and a published GitHub Pages deployment path.
2. FR-2: Antora must become the canonical documentation system for repository feature docs.
3. FR-3: The documentation must cover installation, prerequisites, Redis topology setup, and required environment variables.
4. FR-4: The documentation must fully cover core schema, field-definition, index lifecycle, and YAML schema workflows.
5. FR-5: The documentation must fully cover query APIs including filter, text, count, vector, vector-range, hybrid, aggregate-hybrid, multi-vector, aggregation, pagination, and runtime vector options.
6. FR-6: The documentation must fully cover document lifecycle workflows including load, fetch, clear, drop, JSON partial updates, and HASH partial updates.
7. FR-7: The documentation must fully cover higher-level workflows including embeddings cache, semantic cache, semantic router, message history, and semantic message history.
8. FR-8: The documentation must fully cover extension packages and provider-backed integrations, with credentials documented only as required environment variables.
9. FR-9: The documentation must include one working example per major feature area.
10. FR-10: Major feature areas must include at least: core schema/index/document workflows, query/aggregation workflows, cache workflows, message-history/router workflows, provider vectorizer workflows, reranker workflows, and CLI workflows.
11. FR-11: The docs site must include clear navigation between conceptual docs, API-oriented docs, examples, testing guidance, and troubleshooting guidance.
12. FR-12: The GitHub Actions workflow must build and publish the site to GitHub Pages from the default branch.
13. FR-13: The repository must define validation steps for Antora site generation and example compilation.
14. FR-14: Superseded planning artifacts must be moved into `completed-prds/`, while the new active PRD remains in `tasks/`.

## Non-Goals

- Publishing the library to NuGet as part of this effort.
- Redesigning the public API solely for documentation symmetry.
- Adding new provider integrations beyond the currently supported extension packages unless required to fix a broken documented example.
- Replacing the existing test suite with a docs-specific framework.
- Building an interactive API browser or a separate docs application outside Antora and GitHub Pages.
- Writing or generating provider API keys or secrets.

## Design Considerations

- Keep the Antora navigation shallow enough that new users can reach a runnable example within a few clicks.
- Preserve repository-root discoverability by keeping `README.md` focused on orientation and linking into Antora as the primary docs entry point.
- Use task-oriented pages first and API-reference-style pages second; the library already has strong type names, but the main documentation gap is workflow guidance.
- Treat example pages as first-class documentation pages rather than appendix material.

## Technical Considerations

- Existing markdown under `docs/`, `examples/`, and `README.md` should be treated as source material for migration, not automatically duplicated verbatim.
- GitHub Pages deployment should use the supported GitHub Actions Pages flow rather than custom branch-push logic where possible.
- Provider-backed examples must consume credentials from environment variables and should document expected behavior when variables are unset.
- Example coverage should align with current project structure to avoid creating redundant samples that drift from real code.
- Docs validation should fit alongside the current .NET build and test workflows without requiring maintainers to manage a second opaque pipeline.
- The namespace rename from `RedisVlDotNet` to `RedisVl` in public code should be reflected consistently in all user-facing code snippets while project-path references remain accurate.

## Success Metrics

- A new user can go from repository root to a published getting-started flow and run a basic example without reading source files.
- Every major feature area has one documented working example linked from the Antora navigation.
- The GitHub Pages site builds and publishes successfully from GitHub Actions.
- Antora build validation and example compilation become repeatable repository quality gates.
- Documentation-related questions that currently require source-code lookup can be answered from the published docs alone.

## Open Questions

- Whether the repository should keep a small set of top-level markdown files after Antora migration or reduce almost all feature docs to Antora-only content.
- Whether generated API reference content should be added later, or whether hand-authored workflow/reference pages are sufficient for the first Antora release.
- Whether example verification in CI should remain build-only or expand to selective runnable integration flows for non-provider examples.
- Whether GitHub Pages should publish only the latest docs or reserve room for versioned Antora documentation later.
