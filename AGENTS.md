# OilErp Knowledge Brief

## 1. Mission, Architecture & Runtime Surfaces
- **Purpose**: centralize asset integrity, corrosion analytics, risk policies, and plant event ingestion for oil plants (ANPZ, KRNPZ, SNPZ). Central hub exposes analytical SQL while each plant pushes measurements via stored procedures (docs/architecture.md).
- **Runtime topology**: PostgreSQL per plant + central DB (`sql/` DDL), .NET kernel (`src/OilErp.Core` + `src/OilErp.Infrastructure`), smoke-runner CLI/tests (`src/OilErp.Tests.Runner`), Avalonia desktop UI (`src/OilErp.Ui`). Event flow uses plant outbox -> central inbox with helper functions in `sql/central` and `sql/anpz|krnpz`.
- **Primary operations**: strongly named in `src/OilErp.Core/Operations/OperationNames.cs` and mapped to SQL in `src/OilErp.Infrastructure/Readme.Mapping.md`. Keep this mapping authoritative when adding SQL.

## 2. Repository Layout & Responsibilities
- `src/OilErp.sln`: master solution; `OilErp.Ui.sln` is UI-only. Build with `dotnet build src/OilErp.sln -c Release`.
- `src/OilErp.Core`: contracts (`Contracts/`), DTOs (`Dto/`), base abstractions (`Abstractions/`), SQL wrapper services under `Services/` (Central, Plants, Aggregations, Dtos). Example: `Services/Central/FnAssetSummaryJsonService.cs` handles `public.fn_asset_summary_json`, `Services/Aggregations/PlantCrService.cs` composes analytics without new SQL.
- `src/OilErp.Infrastructure`: adapters that make Core portable. `Adapters/StorageAdapter.cs` (Npgsql-based `IStoragePort`) inspects pg catalog for routines, builds commands, handles LISTEN/NOTIFY, transactions, and JSON results. `Adapters/KernelAdapter.cs` exposes `ICoreKernel`. Config primitives live in `Config/`.
- `src/OilErp.Tests.Runner`: console harness combining smoke scenarios and an operator CLI (`Program.cs`). Tests cover kernel health, ingestion, analytics, async cancellation, schema validation. CLI commands (`add-asset`, `add-measurements-anpz`, `events-*`, `summary`, `top-by-cr`, `eval-risk`, `plant-cr`, `watch`) call the same services to touch the database. `TestDoubles/` hosts fake storage ports for deterministic tests.
- `src/OilErp.Ui`: Avalonia 11 desktop shell following MCP7 layout (docs/ui-mcp7-plan.md). View models under `ViewModels/`, services under `Services/` (KernelGateway, MeasurementDataProvider, MeasurementSnapshotService, InMemoryStoragePort). Fallback measurement JSON lives in repo root (`*_measurements.json`) and is linked into the app output (`OilErp.Ui.csproj`). Views rely on `ThemePalette` and sections defined in `MainWindowViewModel`.
- `sql/`: idempotent DDL/DML packs for central and plant DBs (see `sql/README.md`). Apply in prescribed order per directory.
- `docs/`: execution guide (`docs/README.md`), system architecture, database design, UI outline.

## 3. Data & Operation Flow
1. **Plant ingestion**: local apps call `public.sp_insert_measurement_batch` (`OperationNames.Plant.MeasurementsInsertBatch`). Triggers write to `local_events`; FDW staging pushes payloads into central inbox. Use `src/OilErp.Core/Services/Plants/*/SpInsertMeasurementBatch*.cs` for strongly typed access.
2. **Central queue**: `fn_events_enqueue`, `fn_events_peek`, `fn_ingest_events`, `fn_events_requeue`, `fn_events_cleanup` coordinate event lifecycle. CLI commands `events-*` (Program.cs) and smoke tests (`StorageSmoke`) exercise these flows.
3. **Analytics**: `fn_asset_summary_json`, `fn_top_assets_by_cr`, `fn_eval_risk`, `fn_calc_cr` feed dashboards. Aggregator services (`PlantCrService`, `AnalyticsService`, `MeasurementDataProvider`) build higher-level DTOs for UI/tests.
4. **Risk/policy ops**: `fn_policy_upsert`, `fn_asset_upsert` (and their `sp_...` companions) seeded via CLI `add-asset`, UI `AddOperationFormViewModel`, and tests (`CentralHealthCheckScenario` inside `StorageSmoke.cs`).
5. **Notifications**: `StorageAdapter` supports LISTEN/NOTIFY so tools (CLI `watch` command) or future UI diagnostics can subscribe to channels without extra infrastructure.

## 4. Build, Test, and Verification
- **Restore**: `dotnet restore src/OilErp.sln`.
- **Full build**: `dotnet build src/OilErp.sln -c Release` (warnings as errors per `Directory.Build.props`).
- **Smoke runner**: `dotnet run --project src/OilErp.Tests.Runner` (interactive menu). Append CLI commands after `--` for operational tasks, e.g. `dotnet run --project src/OilErp.Tests.Runner -- summary --asset A-001`.
- **UI**: `dotnet run --project src/OilErp.Ui` (requires GUI environment; uses Avalonia). Launches even without DB by falling back to snapshot JSON.
- **Formatting (optional)**: `dotnet format src/OilErp.sln`.
- **Manual SQL**: apply scripts in `sql/central` then plant directories. Keep Postgres 14+ for FDW support.

## 5. Environment & Configuration
- Preferred configuration source is environment variables (docs/README.md):  
  - CLI/tests expect `OILERP__DB__CONN` and optional `OILERP__DB__TIMEOUT_SEC`.  
  - UI uses `KernelGateway` (`src/OilErp.Ui/Services/KernelGateway.cs`) and looks for `OIL_ERP_PG` plus `OIL_ERP_PG_TIMEOUT`. Provide a single PostgreSQL connection string that reaches the central DB with rights to call listed routines.
- Alternate: `appsettings.Development.json` adjacent to `OilErp.Tests.Runner` for CLI/tests.
- Always keep secrets out of repo; rely on user env/secret managers.

## 6. UI Implementation Plan (Real Data & End-to-End Flow)
> Goal: make the Avalonia shell a production control room powered by live DB data, without losing offline fallbacks.

1. **Connectivity contract**  
   - Ensure the same Postgres profile used by CLI tests is reachable. Either export both env vars (`OILERP__DB__CONN` for tests + `OIL_ERP_PG` for UI) to point at the central DB, or update `KernelGateway.Create` to read `TestEnvironment.LoadStorageConfig()` for consistency.  
   - Use `dotnet run --project src/OilErp.Tests.Runner -- Kernel_Opens_Connection` (default scenario) to validate before UI work. This prevents UI coupling issues later.

2. **Live data sourcing for dashboards**  
   - `MeasurementDataProvider.Load()` already attempts live fetch via `FnTopAssetsByCrService` and `FnAssetSummaryJsonService` before falling back to JSON (`MeasurementSnapshotService`). Confirm `KernelGateway.IsLive` toggles accordingly and bubble `DataSourceStatus` into the shell (see `MainWindowViewModel` initialization).  
   - Extend provider with additional DTO builders (e.g., include risk levels from `AssetSummaryDto.Risk`) so the Overview, Plants, Analytics panels can render actual severities instead of placeholder text. Back this with new view-model projections.

3. **Measurement capture tied to plant procedures**  
   - `AddMeasurementFormViewModel` currently updates only in-memory collections. Introduce an `IMeasurementIngestionService` that serializes entered points into the JSON expected by `public.sp_insert_measurement_batch`/`..._prc` (`src/OilErp.Core/Services/Plants/*/SpInsertMeasurementBatch*.cs`).  
   - Delegate actual execution to the plant-specific service based on selected plant. Wrap in explicit transactions via `IStoragePort.BeginTransactionAsync` if batching multiple assets.  
   - After a successful call, refresh analytics by reusing `MeasurementDataProvider.LoadFromKernel` to confirm data landed, and append to diagnostics feed.

4. **Operations pane wired to central commands**  
   - `AddOperationFormViewModel` already forms `CommandSpec` for asset/policy/event operations. When running live, it uses the same `IStoragePort` as the rest of the UI. Harden validation (trim/uppercase plant codes, default policy) and surface server feedback in `OperationStatus`.  
   - For richer summaries, incorporate `PlantCrService` and `FnEvalRiskService` results into MCP sections. Example: display `CrMean` + `CrP90` per plant and highlight risk statuses based on `CrP90` thresholds.

5. **Diagnostics & event monitoring**  
   - Reuse `StorageAdapter.SubscribeAsync` via the UI when live connectivity is available: add a diagnostics view-model that subscribes to channels such as `events_ingest` and appends notifications to `Diagnostics`.  
   - Mirror CLI `watch` command semantics inside the UI to provide operator visibility without leaving the desktop app.

6. **Verification loop**  
   - After wiring UI actions, always re-run smoke scenarios (especially `Central_Bulk_Data_Seed_And_Analytics`, `Central_Event_Queue_Integrity`, `Database_Inventory_Matches_Expectations`) to catch breaking changes.  
   - Capture representative UI flows (screen/video) and link to `docs/ui-mcp7-plan.md` updates whenever UX/behavior shifts.

## 7. Coding Standards & Guardrails
- C# 12, .NET 8, nullable + implicit usings enabled (see csproj files). 4-space indent, PascalCase for types/members, `_camelCase` private fields, camelCase locals/params.
- Warnings-as-errors enforced; fix analyzers before commit.
- Keep one type per file. Place new Core contracts/DTOs/services in their respective folders, adapters/configs under Infrastructure, view models/services in the UI project.
- Update `src/OilErp.Infrastructure/Readme.Mapping.md` and relevant SQL files simultaneously when adding operations. Reflect behavior or schema changes in `docs/` and `sql/README.md`.
- Tests: prefer `OilErp.Tests.Runner` scenarios using `FakeStoragePort`/`TransactionalFakeStoragePort` for deterministic coverage. Register new scenarios via `Program.cs` by calling `runner.Register(...)`.
- Security: never commit credentials; SQL scripts must stay idempotent and reversible. Leverage PostgreSQL roles/FDW placeholders as in `sql/anpz/02_fdw.sql`.

This brief is the canonical onboarding doc for AI/agents working in `/Users/Ilia/RiderProjects/KursovayaRabotaProject/kursovaya_rabota_project`. Keep it up to date as architecture evolves.
