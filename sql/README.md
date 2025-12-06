# SQL пакеты

## central/
- `01_tables.sql` — таблицы/индексы: `assets_global`, `risk_policies`, `analytics_cr`, `events_inbox`.
- `02_functions_core.sql` — функции:
  - `fn_calc_cr(prev_thk, prev_date, last_thk, last_date)`
  - `fn_asset_upsert`, `fn_policy_upsert` (обёртки, делегируют в процедуры)
  - Очередь: `fn_events_enqueue/peek/ingest/requeue/cleanup`
  - Аналитика: `fn_eval_risk`, `fn_asset_summary_json`, `fn_top_assets_by_cr`, `fn_plant_cr_stats`
- `03_procedures.sql` — основная логика мутаций (OUT параметры):
  - `sp_ingest_events(p_limit, OUT processed)` — принимает только события `HC_MEASUREMENT_BATCH` с валидным asset_code/датами/толщинами, обновляет `analytics_cr`, ставит `processed_at`, шлёт `NOTIFY events_ingest`
  - `sp_events_enqueue(p_event_type, p_source_plant, p_payload, OUT p_id)`
  - `sp_events_requeue(p_ids, OUT n)`, `sp_events_cleanup(p_older_than, OUT n)`
  - `sp_policy_upsert`, `sp_asset_upsert` (OUT id)
- `99_mass_test_events.sql` — вспомогательный генератор нагрузки (ручной запуск).

Порядок применения: 01 → 02 → 03. (Legacy 04 удалён.)

## anpz/krnpz/
- `01_tables.sql` — `assets_local`, `measurement_points`, `measurements`, `local_events`.
- `02_fdw.sql` — `postgres_fdw`, сервер `central_srv`, внешняя таблица `central_ft.events_inbox`.
- `03_trigger_measurements_ai.sql` — триггер AFTER INSERT → `local_events`.
- `04_function_sp_insert_measurement_batch.sql` — FUNCTION-валидация/нормализация точек, вызывает процедуру.
- `05_procedure_wrapper.sql` — PROCEDURE (OUT p_inserted) вставляет данные и публикует событие `HC_MEASUREMENT_BATCH` в central inbox.

## Карта OperationNames → файлы (central)
- `Central.CalcCr` → `02_functions_core.sql` (fn_calc_cr)
- `Central.AssetUpsert`/`SpAssetUpsert` → `02_functions_core.sql` / `03_procedures.sql`
- `Central.PolicyUpsert`/`SpPolicyUpsert` → `02_functions_core.sql` / `03_procedures.sql`
- `Central.EventsEnqueue/EventsPeek/EventsRequeue/EventsCleanup/EventsIngest` → `02_functions_core.sql` (fn) / `03_procedures.sql` (sp)
- `Central.EvalRisk/AnalyticsAssetSummary/AnalyticsTopAssetsByCr/AnalyticsPlantCrStats` → `02_functions_core.sql`
- `Plant.MeasurementsInsertBatch/MeasurementsInsertBatchPrc` → `anpz|krnpz/04_*` / `05_*`

## Особенности
- Идемпотентность: IF NOT EXISTS/OR REPLACE во всех скриптах.
- Event type для заводских событий — `HC_MEASUREMENT_BATCH` (должен совпадать с фильтром ingest).
- Инвентаризация на старте (UI/Tests) сверяет наличие и сигнатуры объектов, может попытаться автоприменить профильные скрипты из `sql/`.
