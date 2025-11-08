# Repository Guidelines

## Project Structure & Module Organization
- `src/OilErp.sln`: .NET solution (C# 12, .NET 8).
- `src/OilErp.Core`: domain contracts, DTOs, operation names, base abstractions.
- `src/OilErp.Infrastructure`: adapters (e.g., `KernelAdapter`, `StorageAdapter`) and config.
- `src/OilErp.Tests.Runner`: console-based smoke tests and fakes.
- `sql/`: PostgreSQL DDL/DML for plants and central DB; see `sql/README.md`.
- `docs/`: architecture and database design notes.

## Build, Test, and Development Commands
- Build solution: `dotnet build src/OilErp.sln -c Release`
- Run smoke tests: `dotnet run --project src/OilErp.Tests.Runner`
- Format code (optional): `dotnet format src/OilErp.sln`
- Restore packages: `dotnet restore src/OilErp.sln`

## Coding Style & Naming Conventions
- C#: 4-space indent, `Nullable` enabled, `ImplicitUsings` enabled, LangVersion 12.
- Names: classes/interfaces/enums PascalCase (`ICoreKernel`), methods/properties PascalCase, fields `_camelCase`, locals/params camelCase, constants PascalCase.
- Organization: place new contracts under `Core/Contracts`, DTOs under `Core/Dto`, adapters under `Infrastructure/Adapters`. Keep files one type per file.
- Warnings: repo treats warnings as errors (`Directory.Build.props`). Fix analyzers before commit.

## Testing Guidelines
- Framework: custom console runner (no xUnit). Add tests under `src/OilErp.Tests.Runner/Smoke` or a new folder.
- Pattern: methods returning `Task<TestResult>` with a descriptive name (e.g., `TestQuerySpecEmptyParametersAllowed`).
- Registration: add each test in `Program.cs` via `runner.Register("Name", instance.TestXxx);`.
- Fakes: prefer `FakeStoragePort`/`TransactionalFakeStoragePort` for isolation; keep tests deterministic and fast.
- Run: `dotnet run --project src/OilErp.Tests.Runner` and verify summary.

## Commit & Pull Request Guidelines
- Commits: imperative, concise subject (<= 72 chars). Optional scope prefix: `core:`, `infra:`, `tests:`, `sql:`.
- Body: explain rationale and impact; reference related docs/SQL mapping if relevant.
- PRs: clear description, linked issues, before/after or sample runner output, and notes on SQL changes (schemas affected). Update `docs/` when behavior changes.

## Security & Configuration Tips
- Do not commit secrets or connection strings. Keep SQL scripts idempotent and reversible.
- When adding operations, map `OperationNames` to SQL in `src/OilErp.Infrastructure/Readme.Mapping.md` and align with `sql/` paths.
