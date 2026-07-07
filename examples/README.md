# Examples

Use this directory as the entry point for runnable `redis-vl-dotnet` samples.

## Prerequisites

All examples currently assume:

- .NET 9 SDK
- Redis 8 or another Redis deployment with RediSearch enabled
- `REDIS_VL_REDIS_URL` set when Redis is not reachable at `localhost:6379`
- `REDIS_VL_REDIS_CLUSTER_NODES` when you want the JSON example to use cluster discovery instead of a direct endpoint

Examples that use JSON storage also require RedisJSON support.

Start Redis locally from the repository root if needed:

```bash
docker compose -f docker-compose.integration.yml up -d redis
export REDIS_VL_REDIS_URL=localhost:6379
```

## Available Examples

## Feature Coverage

Use this map when you want the fastest path to a parity feature area:

| Feature area | Antora entry point | Example or command | Runtime requirements |
| --- | --- | --- | --- |
| Advanced schema options, YAML loading, from-existing index, index listing, JSON partial updates, `TextQuery`, aggregation, clear helper | [Core Features](../docs-site/modules/ROOT/pages/core-features/index.adoc) | [JsonStorageExample](./JsonStorageExample/README.md) | RediSearch + RedisJSON. Supports `REDIS_VL_REDIS_URL` or `REDIS_VL_REDIS_CLUSTER_NODES` |
| Vector query basics, runtime vector search tuning, `MultiVectorQuery`, aggregate hybrid search, HASH partial updates | [Core Features](../docs-site/modules/ROOT/pages/core-features/index.adoc) | [VectorSearchExample](./VectorSearchExample/README.md) | RediSearch with vector similarity support and optional `REDIS_VL_REDIS_URL` |
| SVS-VAMANA vector index, vector compression (LVQ/LeanVec), SVS query-time runtime knobs | [Field Definitions](../docs-site/modules/ROOT/pages/core-features/field-definitions.adoc) | [SvsVamanaExample](./SvsVamanaExample/README.md) | Redis 8.x with RediSearch vector support and optional `REDIS_VL_REDIS_URL` |
| Native `FT.HYBRID` hybrid search, linear/RRF fusion, vector pre-filter, typed mapping | [HybridSearchQuery](../docs-site/modules/ROOT/pages/core-features/hybrid-search-query.adoc) | [HybridSearchExample](./HybridSearchExample/README.md) | Redis 8.4+ with RediSearch and optional `REDIS_VL_REDIS_URL` |
| Exact-input embedding reuse with TTL | [EmbeddingsCache](../docs-site/modules/ROOT/pages/core-features/embeddings-cache.adoc) | [EmbeddingsCacheExample](./EmbeddingsCacheExample/README.md) | Basic Redis only with optional `REDIS_VL_REDIS_URL` |
| Semantic message history with recency and semantic retrieval | [SemanticMessageHistory](../docs-site/modules/ROOT/pages/core-features/semantic-message-history.adoc) | [MessageHistoryExample](./MessageHistoryExample/README.md) | RediSearch with vector similarity support and optional `REDIS_VL_REDIS_URL`; no provider credentials required |
| Semantic cache filter fields and metadata payloads | [SemanticCache](../docs-site/modules/ROOT/pages/core-features/semantic-cache.adoc) | [SemanticCacheExample](./SemanticCacheExample/README.md) | RediSearch with vector similarity support and optional `REDIS_VL_REDIS_URL` |
| Semantic route registration and nearest-route matching | [SemanticRouter](../docs-site/modules/ROOT/pages/core-features/semantic-router.adoc) | [SemanticRouterExample](./SemanticRouterExample/README.md) | RediSearch with vector similarity support and optional `REDIS_VL_REDIS_URL`; no provider credentials required |
| OpenAI vectorizer package | [OpenAI Vectorizer](../docs-site/modules/ROOT/pages/extensions/openai-vectorizer.adoc) | [OpenAiVectorizerExample](./OpenAiVectorizerExample/README.md) | RediSearch with vector similarity support, `OPENAI_API_KEY`, and optional `OPENAI_EMBEDDING_MODEL`, `OPENAI_EMBEDDING_DIMENSIONS`, `REDIS_VL_REDIS_URL` |
| Hugging Face vectorizer package | [Hugging Face Vectorizer](../docs-site/modules/ROOT/pages/extensions/huggingface-vectorizer.adoc) | [HuggingFaceVectorizerExample](./HuggingFaceVectorizerExample/README.md) | RediSearch with vector similarity support, `HF_TOKEN`, and optional `HF_EMBEDDING_MODEL`, `REDIS_VL_REDIS_URL` |
| Cohere vectorizer package | [Cohere Vectorizer](../docs-site/modules/ROOT/pages/extensions/cohere-vectorizer.adoc) | [CohereVectorizerExample](./CohereVectorizerExample/README.md) | RediSearch with vector similarity support, `COHERE_API_KEY`, and optional `COHERE_EMBEDDING_MODEL`, `REDIS_VL_REDIS_URL` |
| Microsoft.Extensions.AI vectorizer adapter | [Microsoft.Extensions.AI Vectorizer](../docs-site/modules/ROOT/pages/extensions/extensions-ai-vectorizer.adoc) | [ExtensionsAiVectorizerExample](./ExtensionsAiVectorizerExample/README.md) | `OPENAI_API_KEY` and optional `OPENAI_EMBEDDING_MODEL`, `OPENAI_EMBEDDING_DIMENSIONS`; no Redis required |
| Local/offline ONNX vectorizer package | [ONNX Vectorizer](../docs-site/modules/ROOT/pages/extensions/onnx-vectorizer.adoc) | [OnnxVectorizerExample](./OnnxVectorizerExample/README.md) | Local `model.onnx` and `tokenizer.json` assets exposed through `ONNX_VECTORIZER_MODEL_PATH` and `ONNX_VECTORIZER_TOKENIZER_PATH`; no Redis or provider credentials required |
| Cohere reranker package | [Cohere Reranker](../docs-site/modules/ROOT/pages/extensions/cohere-reranker.adoc) | [CohereRerankerExample](./CohereRerankerExample/README.md) | RediSearch + RedisJSON, `COHERE_API_KEY`, and optional `COHERE_RERANK_MODEL`, `REDIS_VL_REDIS_URL` |
| ONNX reranker package | [ONNX Reranker](../docs-site/modules/ROOT/pages/extensions/onnx-reranker.adoc) | [OnnxRerankerExample](./OnnxRerankerExample/README.md) | Local `model.onnx` and `tokenizer.json` assets exposed through `ONNX_RERANKER_MODEL_PATH` and `ONNX_RERANKER_TOKENIZER_PATH` |
| Microsoft.Extensions.VectorData / Semantic Kernel vector-store connector and chat-memory store | [Vector Data Connector](../docs-site/modules/ROOT/pages/extensions/vector-data-connector.adoc) | [VectorDataConnectorExample](./VectorDataConnectorExample/README.md) | RediSearch + RedisJSON, `OPENAI_API_KEY`, and optional `OPENAI_EMBEDDING_MODEL`, `REDIS_VL_REDIS_URL` |
| Semantic Kernel consuming the RedisVL MEVD connector (`VectorStoreTextSearch`) | [Vector Data Connector](../docs-site/modules/ROOT/pages/extensions/vector-data-connector.adoc) | [SemanticKernelConnectorExample](./SemanticKernelConnectorExample/README.md) | RediSearch + RedisJSON, `OPENAI_API_KEY`, and optional `OPENAI_EMBEDDING_MODEL`, `REDIS_VL_REDIS_URL` |

### [JsonStorageExample](./JsonStorageExample/README.md)

Demonstrates the core JSON workflow:

- define a JSON-backed schema
- create an index
- load sample documents
- fetch a document by id
- run filter, text, and count queries
- clear indexed documents while preserving the index
- drop the example index

Redis prerequisites:

- RediSearch
- RedisJSON

Run it from the repository root:

```bash
dotnet run --project examples/JsonStorageExample/JsonStorageExample.csproj
```

### [VectorSearchExample](./VectorSearchExample/README.md)

Demonstrates the core vector workflow:

- define a HASH-backed schema with a vector field
- seed deterministic float32 embeddings
- run a nearest-neighbor query
- run an aggregate hybrid query over the vector candidates
- inspect returned distances and grouped aggregates
- drop the example index and documents

Redis prerequisites:

- RediSearch with vector similarity support

Run it from the repository root:

```bash
dotnet run --project examples/VectorSearchExample/VectorSearchExample.csproj
```

### [SvsVamanaExample](./SvsVamanaExample/README.md)

Demonstrates the SVS-VAMANA vector index with compression:

- define a HASH-backed schema with an `SvsVamana` vector field
- enable LVQ8 vector compression and build-time graph knobs
- seed deterministic float32 embeddings
- run a nearest-neighbor query with SVS-VAMANA runtime knobs (`searchWindowSize`, `useSearchHistory`, `searchBufferCapacity`)
- run a vector range query with an `epsilon` runtime knob

Redis prerequisites:

- Redis 8.x (or newer) with RediSearch vector support

Run it from the repository root:

```bash
dotnet run --project examples/SvsVamanaExample/SvsVamanaExample.csproj
```

### [HybridSearchExample](./HybridSearchExample/README.md)

Demonstrates the native `FT.HYBRID` hybrid search flow with `HybridSearchQuery`:

- define a HASH-backed schema with text fields, a tag, and an HNSW vector field
- seed deterministic float32 embeddings
- run linear fusion (`COMBINE LINEAR`, explicit `alpha`/`beta`) with `EF_RUNTIME` tuning
- run server-default and explicit reciprocal rank fusion (RRF)
- apply a vector pre-filter that restricts the candidate set before fusion
- project results onto a typed record with `SearchAsync<T>(...)`
- drop the example index and documents

Redis prerequisites:

- Redis 8.4+ with RediSearch (`FT.HYBRID` is unavailable on older servers)

Run it from the repository root:

```bash
dotnet run --project examples/HybridSearchExample/HybridSearchExample.csproj
```

### [EmbeddingsCacheExample](./EmbeddingsCacheExample/README.md)

Demonstrates exact-input embedding reuse:

- create an `EmbeddingsCache` with a per-run namespace
- store an embedding for one input string
- look up the cached embedding by the same input
- overwrite the stored embedding and confirm the new value

Run it from the repository root:

```bash
dotnet run --project examples/EmbeddingsCacheExample/EmbeddingsCacheExample.csproj
```

### [MessageHistoryExample](./MessageHistoryExample/README.md)

Demonstrates semantic message history retrieval:

- create a HASH-backed semantic message history index
- append session messages with embeddings and metadata
- retrieve the most recent messages for one session
- retrieve semantically relevant messages within the same session
- drop the example index and documents

Redis prerequisites:

- RediSearch with vector similarity support

Run it from the repository root:

```bash
dotnet run --project examples/MessageHistoryExample/MessageHistoryExample.csproj
```

### [SemanticCacheExample](./SemanticCacheExample/README.md)

Demonstrates enriched semantic cache retrieval:

- create a HASH-backed semantic cache with filterable fields
- store semantic cache entries with metadata payloads
- keep tenant-specific prompt variants in the same cache
- retrieve a filtered semantic cache hit
- drop the example index and documents

Redis prerequisites:

- RediSearch with vector similarity support

Run it from the repository root:

```bash
dotnet run --project examples/SemanticCacheExample/SemanticCacheExample.csproj
```

### [SemanticRouterExample](./SemanticRouterExample/README.md)

Demonstrates nearest-route matching:

- create a `SemanticRouter` with a local sample vectorizer
- add routes for multiple intent categories
- route a new utterance to the nearest stored route
- drop the example index and documents

Redis prerequisites:

- RediSearch with vector similarity support

Run it from the repository root:

```bash
dotnet run --project examples/SemanticRouterExample/SemanticRouterExample.csproj
```

### [OpenAiVectorizerExample](./OpenAiVectorizerExample/README.md)

Demonstrates provider-backed vectorization with `SemanticCache`:

- create a HASH-backed semantic cache sized for the configured OpenAI embedding dimensions
- generate seed embeddings through the OpenAI extension package in one batch request
- store a semantic cache entry with the generated embedding
- retrieve a semantically similar cache hit by vectorizing a new prompt through OpenAI
- drop the example index and documents

Redis prerequisites:

- RediSearch with vector similarity support

Additional prerequisites:

- `OPENAI_API_KEY`

Run it from the repository root:

```bash
dotnet run --project examples/OpenAiVectorizerExample/OpenAiVectorizerExample.csproj
```

### [HuggingFaceVectorizerExample](./HuggingFaceVectorizerExample/README.md)

Demonstrates provider-backed vectorization with `SemanticCache`:

- create a HASH-backed semantic cache sized from a live Hugging Face embedding response
- generate seed embeddings through the Hugging Face extension package in one batch request
- store a semantic cache entry with the generated embedding
- retrieve a semantically similar cache hit by vectorizing a new prompt through Hugging Face
- drop the example index and documents

Redis prerequisites:

- RediSearch with vector similarity support

Additional prerequisites:

- `HF_TOKEN`

Run it from the repository root:

```bash
dotnet run --project examples/HuggingFaceVectorizerExample/HuggingFaceVectorizerExample.csproj
```

### [CohereVectorizerExample](./CohereVectorizerExample/README.md)

Demonstrates provider-backed vectorization with `SemanticCache` and Cohere's input-type distinction:

- create a HASH-backed semantic cache sized from a live Cohere embedding response
- generate seed embeddings with a `SearchDocument` vectorizer in one batch request
- store a semantic cache entry with the generated embedding
- retrieve a semantically similar cache hit by vectorizing a new prompt with a `SearchQuery` vectorizer
- drop the example index and documents

Redis prerequisites:

- RediSearch with vector similarity support

Additional prerequisites:

- `COHERE_API_KEY`

Run it from the repository root:

```bash
dotnet run --project examples/CohereVectorizerExample/CohereVectorizerExample.csproj
```

### [ExtensionsAiVectorizerExample](./ExtensionsAiVectorizerExample/README.md)

Demonstrates `Microsoft.Extensions.AI` interop through `RedisVL.Vectorizers.ExtensionsAI`:

- create an OpenAI `EmbeddingClient`
- convert it to `IEmbeddingGenerator<string, Embedding<float>>`
- wrap it with `ExtensionsAiTextVectorizer`
- vectorize one input and a batch of inputs through the RedisVL abstraction
- print the resulting vector dimensions and sample values

Additional prerequisites:

- `OPENAI_API_KEY`

Run it from the repository root:

```bash
dotnet run --project examples/ExtensionsAiVectorizerExample/ExtensionsAiVectorizerExample.csproj
```

### [CohereRerankerExample](./CohereRerankerExample/README.md)

Demonstrates text search plus Cohere reranking:

- create a JSON-backed search index with support articles
- retrieve an initial candidate set from Redis with `TextQuery`
- rerank those candidates through the Cohere extension package
- print the original Redis order alongside the Cohere-adjusted order
- drop the example index and documents

Redis prerequisites:

- RediSearch
- RedisJSON

Additional prerequisites:

- `COHERE_API_KEY`

Run it from the repository root:

```bash
dotnet run --project examples/CohereRerankerExample/CohereRerankerExample.csproj
```

### [OnnxRerankerExample](./OnnxRerankerExample/README.md)

Demonstrates local reranking with `RedisVL.Rerankers.Onnx`:

- create an in-memory candidate set
- build `RerankDocument` values from those candidates
- rerank the candidates locally with `OnnxTextReranker`
- print the original order alongside the ONNX-adjusted order

Additional prerequisites:

- `ONNX_RERANKER_MODEL_PATH`
- `ONNX_RERANKER_TOKENIZER_PATH`

Run it from the repository root:

```bash
dotnet run --project examples/OnnxRerankerExample/OnnxRerankerExample.csproj
```

### [OnnxVectorizerExample](./OnnxVectorizerExample/README.md)

Demonstrates local/offline embedding generation with `RedisVL.Vectorizers.Onnx`:

- load a local ONNX SentenceTransformers model and tokenizer
- embed a batch of prompts in a single call with no API key or network access
- print the embedding count and dimensionality
- compare cosine similarity between a paraphrase and an unrelated prompt

Additional prerequisites:

- `ONNX_VECTORIZER_MODEL_PATH`
- `ONNX_VECTORIZER_TOKENIZER_PATH`

Run it from the repository root:

```bash
dotnet run --project examples/OnnxVectorizerExample/OnnxVectorizerExample.csproj
```

### [VectorDataConnectorExample](./VectorDataConnectorExample/README.md)

Demonstrates the `RedisVL.Connectors.VectorData` package — a Microsoft.Extensions.VectorData (MEVD) /
Semantic Kernel-compatible vector-store connector backed by RedisVL:

- create a `RedisVLVectorStore` and a strongly-typed `VectorStoreCollection<string, Movie>`
- map a POCO with `[VectorStoreKey]`, `[VectorStoreData]`, and `[VectorStoreVector]` attributes
- embed summaries and the query with OpenAI through a Microsoft.Extensions.AI `IEmbeddingGenerator`
- upsert records and fetch one by key
- run a vector search with a LINQ metadata pre-filter
- run filtered (non-vector) retrievals with a range of LINQ predicates (equality, range, `&&`, `||`, `Contains`/IN, negation)
- persist and replay chat history with `RedisVLChatMessageStore`

Redis prerequisites:

- RediSearch with vector similarity support
- RedisJSON

Additional prerequisites:

- `OPENAI_API_KEY`

Run it from the repository root:

```bash
dotnet run --project examples/VectorDataConnectorExample/VectorDataConnectorExample.csproj
```

### [SemanticKernelConnectorExample](./SemanticKernelConnectorExample/README.md)

Demonstrates Semantic Kernel consuming the RedisVL MEVD connector — the same `RedisVLVectorStore`
plugged into SK's `VectorStoreTextSearch<T>`:

- wrap a `RedisVLCollection<string, Movie>` in `VectorStoreTextSearch<Movie>`
- embed records and queries with OpenAI through a Microsoft.Extensions.AI `IEmbeddingGenerator`
- run `GetTextSearchResultsAsync(...)` for RAG retrieval over RedisVL
- run a LINQ-filtered text search (`TextSearchOptions<Movie>.Filter`) routed through to the connector
- expose the search to a `Kernel` as a plugin

Redis prerequisites:

- RediSearch with vector similarity support
- RedisJSON

Additional prerequisites:

- `OPENAI_API_KEY`

Run it from the repository root:

```bash
dotnet run --project examples/SemanticKernelConnectorExample/SemanticKernelConnectorExample.csproj
```

## Related Docs

- [Getting Started](../docs-site/modules/ROOT/pages/getting-started/index.adoc)
- [Examples](../docs-site/modules/ROOT/pages/examples/index.adoc)
- [Testing](../docs-site/modules/ROOT/pages/testing/index.adoc)
