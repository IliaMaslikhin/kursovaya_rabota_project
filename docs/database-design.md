# Дизайн базы данных

## Центральная БД (public)
- **assets_global**: `asset_code` (UNIQUE), `name`, `type`, `plant_code`, `created_at`.
- **risk_policies**: `name` (UNIQUE), пороги `threshold_low/med/high`.
- **analytics_cr**: расчёт CR и последние/предыдущие замеры, FK на `assets_global.asset_code`.
- **measurement_batches**: журнал батчей замеров (`source_plant`, `asset_code`, `prev_*`, `last_*`, `created_at`).

## Основные функции/процедуры (central)
- `fn_calc_cr` — CR по двум замерам.
- `fn/sp_asset_upsert`, `fn/sp_policy_upsert` — upsert справочников (функции делегируют в процедуры, OUT id).
- Триггер `trg_measurement_batches_bi` (через `trg_measurement_batches_bi_fn`) — при вставке в `measurement_batches` обновляет `assets_global` и `analytics_cr`.
- Аналитика: `fn_eval_risk`, `fn_asset_summary_json`, `fn_top_assets_by_cr`, `fn_plant_cr_stats`.

## Заводские БД (ANPZ/KRNPZ, public)
- **assets_local**, **measurement_points**, **measurements** (CHECK thickness > 0), **local_events**.
- FDW: `central_srv` + `central_ft.measurement_batches` для прямой записи батчей в central.
- Триггер: `trg_measurements_ai` → пишет в `local_events` при вставке замера.
- Измерения: `sp_insert_measurement_batch` (FUNCTION) валидирует/нормализует точки, вызывает `sp_insert_measurement_batch_prc`.  
  `sp_insert_measurement_batch_prc` (PROCEDURE, OUT p_inserted) вставляет актив/точки/замеры и пишет агрегированный батч (prev/last) в FDW таблицу central.

## Порядок применения скриптов
- central: `01_tables.sql` → `02_functions_core.sql` → `03_procedures.sql` (legacy 04 не используется).  
- anpz/krnpz: `01_tables.sql` → `02_fdw.sql` → `03_trigger_measurements_ai.sql` → `04_function_sp_insert_measurement_batch.sql` → `05_procedure_wrapper.sql`.
- Нагрузочный генератор: `central/99_mass_test_events.sql` (ручной запуск).

## Особенности
- Все операции описаны в `src/OilErp.Core/Operations/OperationNames.cs` и `src/OilErp.Infrastructure/Readme.Mapping.md`; при добавлении SQL обновлять карту и инвентаризацию.
- Инвентаризация при старте (UI/Tests) проверяет наличие объектов и сигнатуры (pg_proc) для каждого профиля.
- Поток данных без очереди: завод → FDW вставка в `measurement_batches` → триггер central обновляет `analytics_cr`.
