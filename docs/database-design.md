# Дизайн базы данных

## Центральная БД (public)
- **assets_global**: `asset_code` (UNIQUE), `name`, `type`, `plant_code`, `created_at`.
- **risk_policies**: `name` (UNIQUE), пороги `threshold_low/med/high`.
- **analytics_cr**: расчёт CR и последние/предыдущие замеры, FK на `assets_global.asset_code`.
- **events_inbox**: очередь событий (`event_type`, `source_plant`, `payload_json`, `created_at`, `processed_at`).

## Основные функции/процедуры (central)
- `fn_calc_cr` — CR по двум замерам.
- `fn/sp_asset_upsert`, `fn/sp_policy_upsert` — upsert справочников (функции делегируют в процедуры, OUT id).
- Очередь: `fn_events_enqueue`, `fn_events_peek`, `fn_events_requeue`, `fn_events_cleanup`.
- Инжест: `fn_ingest_events` → `sp_ingest_events(p_limit, OUT processed)`: берёт только `HC_MEASUREMENT_BATCH` с валидным `asset_code/last_thk/last_date`, проверяет порядок дат/толщин, обновляет `analytics_cr`, ставит `processed_at`, шлёт `NOTIFY events_ingest`.
- Аналитика: `fn_eval_risk`, `fn_asset_summary_json`, `fn_top_assets_by_cr`, `fn_plant_cr_stats`.

## Заводские БД (ANPZ/KRNPZ, public)
- **assets_local**, **measurement_points**, **measurements** (CHECK thickness > 0), **local_events**.
- FDW: `central_srv` + `central_ft.events_inbox` для записи в центральную очередь.
- Триггер: `trg_measurements_ai` → пишет в `local_events` при вставке замера.
- Измерения: `sp_insert_measurement_batch` (FUNCTION) валидирует/нормализует точки, вызывает `sp_insert_measurement_batch_prc`.  
  `sp_insert_measurement_batch_prc` (PROCEDURE, OUT p_inserted) вставляет актив/точки/замеры и публикует событие `HC_MEASUREMENT_BATCH` в FDW inbox.

## Порядок применения скриптов
- central: `01_tables.sql` → `02_functions_core.sql` → `03_procedures.sql` (legacy 04 не используется).  
- anpz/krnpz: `01_tables.sql` → `02_fdw.sql` → `03_trigger_measurements_ai.sql` → `04_function_sp_insert_measurement_batch.sql` → `05_procedure_wrapper.sql`.
- Нагрузочный генератор: `central/99_mass_test_events.sql` (ручной запуск).

## Особенности
- Все операции описаны в `src/OilErp.Core/Operations/OperationNames.cs` и `src/OilErp.Infrastructure/Readme.Mapping.md`; при добавлении SQL обновлять карту и инвентаризацию.
- Инвентаризация при старте (UI/Tests) проверяет наличие объектов и сигнатуры (pg_proc) для каждого профиля.
- Event type для заводских событий: `HC_MEASUREMENT_BATCH` (должен совпадать с фильтром central ingest).
