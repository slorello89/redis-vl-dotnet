# Message History Example

Demonstrates semantic message history retrieval:

- create a semantic message history index with a vector field
- append session messages with generated embeddings and metadata payloads
- retrieve the most recent messages for a session
- retrieve semantically relevant messages for a prompt within the same session
- clean up the example index and documents

Run it from the repository root:

```bash
dotnet run --project examples/MessageHistoryExample/MessageHistoryExample.csproj
```
