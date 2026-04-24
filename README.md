# redis-vl-dotnet

`redis-vl-dotnet` is a .NET-native Redis Vector Library for Redis Search and vector workloads.

The canonical documentation now lives in the Antora site source at [docs-site/modules/ROOT/pages/index.adoc](docs-site/modules/ROOT/pages/index.adoc). Start there for the current overview, getting-started flow, examples map, CLI guidance, and validation instructions.

## Documentation

- Canonical docs entry point: [docs-site/modules/ROOT/pages/index.adoc](docs-site/modules/ROOT/pages/index.adoc)
- Getting started overview: [docs-site/modules/ROOT/pages/getting-started/index.adoc](docs-site/modules/ROOT/pages/getting-started/index.adoc)
- Core features overview: [docs-site/modules/ROOT/pages/core-features/index.adoc](docs-site/modules/ROOT/pages/core-features/index.adoc)
- Extensions overview: [docs-site/modules/ROOT/pages/extensions/index.adoc](docs-site/modules/ROOT/pages/extensions/index.adoc)
- Testing and validation: [docs-site/modules/ROOT/pages/testing/index.adoc](docs-site/modules/ROOT/pages/testing/index.adoc)
- Runnable examples index: [examples/README.md](examples/README.md)

Build the docs locally from the repository root:

```bash
npm install
npm run docs:validate
```

GitHub Pages publishing runs from `.github/workflows/docs-pages.yml` on pushes to `main`. Configure the repository Pages source to `GitHub Actions` before expecting deployments.

## NuGet Releases

The publishable packages are:

- `RedisVL`
- `RedisVL.Vectorizers.Abstractions`
- `RedisVL.Vectorizers.OpenAI`
- `RedisVL.Vectorizers.HuggingFace`
- `RedisVL.Rerankers.Abstractions`
- `RedisVL.Rerankers.Cohere`

The CLI at `src/RedisVL.Cli` is intentionally excluded from NuGet packaging.

Manual NuGet publishing runs from `.github/workflows/nuget-release.yml` via `workflow_dispatch`. Each package is released independently by selecting the target package in the action UI. The workflow restores, builds, tests, packs only the selected project, uploads the `.nupkg` and `.snupkg` artifacts, and then pushes the package to NuGet using the `NUGET_API_KEY` repository secret.

Package versions live in the individual library `.csproj` files. Update the version in the project you intend to release before triggering the workflow.

## Repository Pointers

- Active parity roadmap: [docs/parity-roadmap.md](docs/parity-roadmap.md)
- CLI project: `src/RedisVL.Cli`
- Solution file: `redis-vl-dotnet.sln`
- Ralph plan and iteration log: `prd.json`, `progress.txt`
