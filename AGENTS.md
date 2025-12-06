# OilErp Knowledge Brief (обновлено)

## 1. Назначение, архитектура, рантайм
- **Цель**: учёт активов/коррозии, политики риска, приём событий от заводов (ANPZ, KRNPZ), аналитические SQL в центре.
- **Топология**: PostgreSQL central + заводы (sql/), .NET ядро (`OilErp.Core` + `OilErp.Infrastructure`), смоук/CLI (`OilErp.Tests.Runner`), Avalonia UI (`OilErp.Ui`). Поток: заводская процедура → FDW `central_ft.events_inbox` → central ingest (`fn/sp_ingest_events`) → `analytics_cr`.
- **Операции**: перечислены в `src/OilErp.Core/Operations/OperationNames.cs`, карта в `src/OilErp.Infrastructure/Readme.Mapping.md`. Central функции делегируют в процедуры с OUT. Заводские события — `HC_MEASUREMENT_BATCH`.

## 2. Структура репозитория
- `src/OilErp.Core`: контракты (`IStoragePort/IStorageTransaction` с сейвпоинтами), DTO, базовые сервисы, обёртки SQL (Central/Plants/Aggregations), нормализация ввода в `AppServiceBase`.
- `src/OilErp.Infrastructure`: `StorageAdapter` (Npgsql, pg_proc-кэш, LISTEN/NOTIFY), `StorageConfigProvider`, общий `AppLogger`, `DatabaseBootstrapper`, `DatabaseInventoryInspector`, `KernelAdapter`.
- `src/OilErp.Tests.Runner`: смоук/CLI `Program.cs`; тесты подключения/ingest/аналитики/FDW/LISTEN/негатив/нагрузка; `MeasurementBatchHelper`, фейковые порты.
- `src/OilErp.Ui`: Avalonia 11; `KernelGateway` использует общий bootstrapper/config; `StoragePortFactory` — общий провайдер строк; `MeasurementDataProvider` асинхронный; панели Analytics/Measurements/Diagnostics слушают те же сервисы.
- `sql/`: central (`01_tables`, `02_functions_core`, `03_procedures`), anpz/krnpz (таблицы, FDW, триггер, batch function+procedure), нагрузочный `99_mass_test_events.sql`.
- `docs/`: архитектура, дизайн БД, гайд по запуску, актуальный план.

## 3. Поток данных и операции
1) **Заводы**: `sp_insert_measurement_batch` валидирует точки и вызывает `sp_insert_measurement_batch_prc` → вставка локально + событие `HC_MEASUREMENT_BATCH` в central inbox (FDW).
2) **Очередь центра**: `fn_events_enqueue/peek/requeue/cleanup`; ingest (`fn/sp_ingest_events`) берёт только `HC_MEASUREMENT_BATCH` с валидными датами/толщинами, считает CR, обновляет `analytics_cr`, ставит `processed_at`, шлёт `NOTIFY events_ingest`.
3) **Аналитика**: `fn_top_assets_by_cr`, `fn_asset_summary_json`, `fn_eval_risk`, `fn_calc_cr`, `fn_plant_cr_stats`; агрегатор `PlantCrService`.
4) **Риск/справочники**: `fn/sp_asset_upsert`, `fn/sp_policy_upsert` (фн делегируют в sp).
5) **UI/CLI**: команды `add-asset/policy`, `add-measurements-anpz`, `events-*`, `summary/top-by-cr/eval-risk/plant-cr`, `watch`; UI отображает live CR/риск/диагностику LISTEN.

## 4. Сборка и проверка
- Build: `dotnet build src/OilErp.sln -c Release` (warnings as errors).
- Smoke/CLI: `dotnet run --project src/OilErp.Tests.Runner` (меню) или команды через `--`.
- UI: `dotnet run --project src/OilErp.Ui` (нужна БД; снапшоты только дополняют пустую БД).
- SQL: применять в порядке (central 01→02→03; anpz/krnpz 01→02→03→04→05; 99 при нагрузке).
- Инвентаризация/Bootstrap: общий код в Infrastructure — создаёт базы, проверяет объекты и сигнатуры, пишет лог в `%APPDATA%/OilErp/logs`, кладёт гайд при ошибке, хранит маркер первого запуска.

## 5. Конфигурация
- Единый провайдер: `StorageConfigProvider` читает `OILERP__DB__CONN[_ANPZ|_KRNPZ]`, `OILERP__DB__TIMEOUT_SEC` (UI также читает `OIL_ERP_PG[_ANPZ|_KRNPZ]`).
- Secrets не коммитить; при отсутствии строк UI/Tests падают (оффлайн-режима нет).

## 6. Тестовый охват
- Подключение/транзакции/LISTEN/NOTIFY; ingest/очередь/аналитика; FDW ANPZ+KRNPZ (fn/prc); негативные кейсы (invalid JSON, пустой asset_code); нагрузка (bulk ingest с откатом); валидация сигнатур объектов по OperationNames/Mapping.

## 7. Замечания по актуальному состоянию
- Ingest проверяет порядок дат/толщин, но бизнес-валидацию можно усилить (thresholds по plant/policy).
- Event type должен оставаться `HC_MEASUREMENT_BATCH` для заводских событий.
- План работ: см. `docs/current_plan.md` (UI/Infra/Core/SQL/Tests статус).
