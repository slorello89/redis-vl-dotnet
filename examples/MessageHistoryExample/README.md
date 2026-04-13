# Message History Example

Demonstrates the base message history workflow:

- create a message history index
- append session messages with role, timestamped sequence order, and metadata payloads
- retrieve the most recent messages for a session
- filter recent retrieval by role
- clean up the example index and documents

Run it from the repository root:

```bash
dotnet run --project examples/MessageHistoryExample/MessageHistoryExample.csproj
```
