# Contributing

Thanks for your interest in DePIN Tracker. This document covers how to build, the coding
conventions, and what we expect in a pull request.

## Getting started

```sh
dotnet build DepinTracker.slnx
dotnet test
dotnet run --project src/DepinTracker.App
```

Requires the .NET 10 SDK (pinned by `global.json`). The solution opens in Visual Studio
2026 or VS Code (C# Dev Kit).

## Conventions

- Respect the Clean-Architecture boundaries: Domain depends on nothing; Application
  defines ports; Infrastructure implements them; the App is the only composition root.
- Keep the dependency surface aligned with `PROJECT_SPECIFICATION.md` — prefer hand-rolled
  primitives over adding packages unless there is a clear need.
- Money is always `decimal`; every serialization/parse boundary uses
  `CultureInfo.InvariantCulture`. Do not enable `InvariantGlobalization` (it breaks WPF).
- All I/O and provider methods are `async` and accept a `CancellationToken`.
- Never silently overwrite imported financial data; preserve provenance.
- Follow `.editorconfig` (file-scoped namespaces, `_camelCase` private fields, 4-space indent).

## Changing the database schema

Add a new numbered migration script under
`src/DepinTracker.Infrastructure/Persistence/Migrations/Scripts/<Store>/` (e.g.
`0002_add_column.sql`). Never modify a script that has already shipped.

## Pull requests

- Keep changes focused and include tests for new behavior (xUnit + FluentAssertions; Moq
  for collaborators, the `TempStore` helper for SQLite integration).
- Ensure `dotnet build` and `dotnet test` are green; CI runs both on Windows.
- Update `CHANGELOG.md` and relevant docs.

## License

By contributing you agree your contributions are licensed under the project's
[Business Source License 1.1](LICENSE).
