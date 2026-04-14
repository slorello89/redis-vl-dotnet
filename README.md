# redis-vl-dotnet

`redis-vl-dotnet` is a .NET-native Redis Vector Library for Redis Search and vector workloads.

The canonical documentation now lives in the Antora site source at [docs-site/modules/ROOT/pages/index.adoc](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/index.adoc). Start there for the current overview, getting-started flow, examples map, CLI guidance, and validation instructions.

## Documentation

- Canonical docs entry point: [docs-site/modules/ROOT/pages/index.adoc](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/index.adoc)
- Getting started overview: [docs-site/modules/ROOT/pages/getting-started/index.adoc](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/getting-started/index.adoc)
- Core features overview: [docs-site/modules/ROOT/pages/core-features/index.adoc](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/core-features/index.adoc)
- Extensions overview: [docs-site/modules/ROOT/pages/extensions/index.adoc](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/extensions/index.adoc)
- Testing and validation: [docs-site/modules/ROOT/pages/testing/index.adoc](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs-site/modules/ROOT/pages/testing/index.adoc)
- Runnable examples index: [examples/README.md](/Users/steve.lorello/projects/redis/redis-vl-dotnet/examples/README.md)

Build the docs locally from the repository root:

```bash
npm install
npm run docs:validate
```

GitHub Pages publishing runs from `.github/workflows/docs-pages.yml` on pushes to `main`. Configure the repository Pages source to `GitHub Actions` before expecting deployments.

## Repository Pointers

- Active parity roadmap: [docs/parity-roadmap.md](/Users/steve.lorello/projects/redis/redis-vl-dotnet/docs/parity-roadmap.md)
- CLI project: `src/RedisVlDotNet.Cli`
- Solution file: `redis-vl-dotnet.sln`
- Ralph plan and iteration log: `prd.json`, `progress.txt`
