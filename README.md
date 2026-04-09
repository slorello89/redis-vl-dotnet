# redis-vl-dotnet

`redis-vl-dotnet` is a .NET-native Redis Vector Library for Redis Search and vector workloads.

The implementation plan for v1 is defined in [docs/v1-architecture.md](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/v1-architecture.md). That document is the contract for feature parity, scope, and public API direction across future Ralph iterations.

## Current Workspace

- `docs/v1-architecture.md`: v1 parity matrix and architecture decisions
- `redis-vl-dotnet.sln`: minimal solution scaffold so `dotnet build` is a valid repository quality gate from the first iteration
- `prd.json`: Ralph execution plan
- `progress.txt`: iteration log and reusable codebase patterns
