# OpenAiVectorizerExample

This example demonstrates using `RedisVl.Vectorizers.OpenAI` with `SemanticCache`.

It:

- creates a semantic cache whose vector dimensions match the configured OpenAI embedding size
- generates seed embeddings in one batch request
- stores a cache entry with the generated embedding
- checks for a semantic cache hit by embedding a new prompt through `OpenAiTextVectorizer`
- drops the example index and documents

## Prerequisites

- .NET 9 SDK
- Redis Stack or another Redis deployment with RediSearch enabled
- `OPENAI_API_KEY`

Optional environment variables:

- `REDIS_VL_REDIS_URL` to override the default Redis connection string of `localhost:6379`
- `OPENAI_EMBEDDING_MODEL` to override the default model of `text-embedding-3-small`
- `OPENAI_EMBEDDING_DIMENSIONS` to override the default embedding size of `256`

If `OPENAI_API_KEY` is not set, the example exits immediately with an explicit environment-variable error instead of attempting a partially configured OpenAI request.

Run it from the repository root:

```bash
dotnet run --project examples/OpenAiVectorizerExample/OpenAiVectorizerExample.csproj
```
