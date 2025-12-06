# Запуск и проверка

## Конфигурация подключения
Предпочтительно через переменные окружения (единый провайдер `StorageConfigProvider`):
```bash
export OILERP__DB__CONN="Host=localhost;Username=postgres;Password=postgres;Database=central"
export OILERP__DB__CONN_ANPZ="Host=localhost;Username=postgres;Password=postgres;Database=anpz"
export OILERP__DB__CONN_KRNPZ="Host=localhost;Username=postgres;Password=postgres;Database=krnpz"
export OILERP__DB__TIMEOUT_SEC=30
```
UI читает `OIL_ERP_PG`/`OIL_ERP_PG_TIMEOUT`, но при ProjectReference на Infrastructure можно использовать ту же переменную `OILERP__DB__CONN`.

Альтернатива для CLI/Tests — `appsettings.Development.json` рядом с `src/OilErp.Tests.Runner` (ключи `OILERP:DB:CONN[_ANPZ|_KRNPZ]`).

## Применение SQL
1) central: `sql/central/01_tables.sql` → `02_functions_core.sql` → `03_procedures.sql`.  
2) anpz/krnpz: `01_tables.sql` → `02_fdw.sql` → `03_trigger_measurements_ai.sql` → `04_function_sp_insert_measurement_batch.sql` → `05_procedure_wrapper.sql`.  
3) (опционально) `sql/central/99_mass_test_events.sql` для нагрузочных вставок.

## Первый запуск/Bootstrap
- При старте CLI/Tests/UI работает общий `DatabaseBootstrapper`: создает базы `central/anpz/krnpz` при необходимости, прогоняет инвентаризацию по профилю, пытается авто-применить скрипты, пишет лог в `%APPDATA%/OilErp/logs`, при ошибке кладёт гайд на рабочий стол. Маркер первого запуска хранится в `%APPDATA%/OilErp/first-run.machine`.
- Инвентаризация сверяет наличие и сигнатуры функций/процедур/триггеров/таблиц.

## Запуск
- **Сборка**: `dotnet build src/OilErp.sln -c Release`.
- **Smoke/CLI**: `dotnet run --project src/OilErp.Tests.Runner` (меню) или команды:
  - `add-asset --id A-001 --name "Pipe #1" --plant ANPZ`
  - `add-measurements-anpz --file ./points.json|csv`
  - `events-peek --limit 10`, `events-ingest --max 5000`, `events-requeue --age-sec 3600`, `events-cleanup --age-sec 86400`
  - `summary --asset A-001 [--policy default]`, `top-by-cr --take 20`, `eval-risk --asset A-001`, `plant-cr --plant ANPZ --from 2024-01-01 --to 2025-01-01`
  - `watch --channel events_ingest`
- **UI**: `dotnet run --project src/OilErp.Ui` (Avalonia). Требует рабочее соединение: MeasurementDataProvider использует live-данные, снапшоты лишь дополняют пустую БД.

## Поведение ingest/аналитики
- Заводы публикуют `HC_MEASUREMENT_BATCH` в central через FDW; central `fn/sp_ingest_events` принимает только этот тип, проверяет порядок дат/толщин и шлёт `NOTIFY events_ingest`.
- Аналитика строится из `analytics_cr`: `fn_top_assets_by_cr`, `fn_asset_summary_json`, `fn_eval_risk`, `fn_plant_cr_stats`.

## Логи и диагностика
- Логи: `%APPDATA%/OilErp/logs/app-*.log`.
- LISTEN/NOTIFY: канал `events_ingest` (ingest), можно слушать через CLI `watch` или Diagnostics в UI.
