# SQL пакеты

## central/
- `01_tables.sql` — таблицы/индексы: `assets_global`, `risk_policies`, `analytics_cr`, `measurement_batches`.
- `02_functions_core.sql` — функции:
  - `fn_calc_cr(prev_thk, prev_date, last_thk, last_date)`
  - `fn_asset_upsert`, `fn_policy_upsert` (обёртки, делегируют в процедуры)
  - Аналитика: `fn_eval_risk`, `fn_asset_summary_json`, `fn_top_assets_by_cr`, `fn_plant_cr_stats`
- `03_procedures.sql` — основная логика мутаций (OUT параметры):
  - триггер `trg_measurement_batches_bi` (через `trg_measurement_batches_bi_fn`) — при вставке в `measurement_batches` гарантирует наличие актива и обновляет `analytics_cr`.
  - `sp_policy_upsert`, `sp_asset_upsert` (OUT id)
- `99_mass_test_events.sql` — вспомогательный генератор нагрузки (ручной запуск).

Порядок применения: 01 → 02 → 03. (Legacy 04 удалён.)

## anpz/krnpz/
- `01_tables.sql` — `assets_local`, `measurement_points`, `measurements`, `local_events`.
- `02_fdw.sql` — `postgres_fdw`, сервер `central_srv`, внешняя таблица `central_ft.measurement_batches`.
- `03_trigger_measurements_ai.sql` — триггер AFTER INSERT → `local_events`.
- `04_function_sp_insert_measurement_batch.sql` — FUNCTION-валидация/нормализация точек, вызывает процедуру.
- `05_procedure_wrapper.sql` — PROCEDURE (OUT p_inserted) вставляет данные и пишет батч напрямую в central через FDW.

## Карта OperationNames → файлы (central)
- `Central.CalcCr` → `02_functions_core.sql` (fn_calc_cr)
- `Central.AssetUpsert`/`SpAssetUpsert` → `02_functions_core.sql` / `03_procedures.sql`
- `Central.PolicyUpsert`/`SpPolicyUpsert` → `02_functions_core.sql` / `03_procedures.sql`
- `Central.EvalRisk/AnalyticsAssetSummary/AnalyticsTopAssetsByCr/AnalyticsPlantCrStats` → `02_functions_core.sql`
- `Plant.MeasurementsInsertBatch/MeasurementsInsertBatchPrc` → `anpz|krnpz/04_*` / `05_*`

## Особенности
- Идемпотентность: IF NOT EXISTS/OR REPLACE во всех скриптах.
- Поток данных: завод → FDW вставка в `central.measurement_batches` → триггер central обновляет `analytics_cr`.
- Инвентаризация на старте (UI/Tests) сверяет наличие и сигнатуры объектов, может попытаться автоприменить профильные скрипты из `sql/`.
