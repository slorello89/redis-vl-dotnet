# Semantic Cache Example

Demonstrates an enriched semantic cache workflow:

- create a HASH-backed semantic cache with explicit filterable fields
- store semantic cache entries with JSON metadata payloads
- keep multiple prompt variants separated by tenant and model filters
- retrieve a semantic cache hit with a composed RedisVL filter
- drop the example index and documents

Redis prerequisites:

- RediSearch with vector similarity support

Run it from the repository root:

```bash
dotnet run --project examples/SemanticCacheExample/SemanticCacheExample.csproj
```
