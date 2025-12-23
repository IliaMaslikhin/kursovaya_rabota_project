# SQL пакеты

## central/
- `01_tables.sql` — таблицы/индексы: `assets_global`, `risk_policies`, `analytics_cr`, `measurement_batches` (включая `last_label`, `last_note`).
- `02_functions_core.sql` — функции:
  - `fn_calc_cr`
  - `fn_asset_upsert`, `fn_policy_upsert` (делегируют в процедуры)
  - аналитика: `fn_eval_risk`, `fn_asset_summary_json`, `fn_top_assets_by_cr`, `fn_plant_cr_stats`
- `03_procedures.sql` — процедуры и триггеры:
  - `sp_asset_upsert`, `sp_policy_upsert`
  - `trg_measurement_batches_bi_fn` + `trg_measurement_batches_bi`
- `99_mass_test_events.sql` — генератор нагрузочных батчей (ручной запуск).

Порядок применения: `01` → `02` → `03`.

## anpz/ и krnpz/
- `01_tables.sql` — локальные таблицы: `assets_local`, `measurement_points`, `measurements`, `local_events`.
- `02_fdw.sql` — `postgres_fdw`, сервер `central_srv`, внешняя таблица `central_ft.measurement_batches`.
- `03_trigger_measurements_ai.sql` — триггер `trg_measurements_ai` (AFTER INSERT → `local_events`).
- `04_function_sp_insert_measurement_batch.sql` — функция‑валидация JSON, вызов процедуры.
- `05_procedure_wrapper.sql` — процедура записи замеров + вставка батча в central через FDW.

Порядок применения: `01` → `02` → `03` → `04` → `05`.

## Карта OperationNames → SQL
- `Central.CalcCr` → `public.fn_calc_cr`
- `Central.AssetUpsert` / `Central.SpAssetUpsert` → `public.fn_asset_upsert` / `public.sp_asset_upsert`
- `Central.PolicyUpsert` / `Central.SpPolicyUpsert` → `public.fn_policy_upsert` / `public.sp_policy_upsert`
- `Central.EvalRisk` → `public.fn_eval_risk`
- `Central.AnalyticsAssetSummary` → `public.fn_asset_summary_json`
- `Central.AnalyticsTopAssetsByCr` → `public.fn_top_assets_by_cr`
- `Central.AnalyticsPlantCrStats` → `public.fn_plant_cr_stats`
- `Plant.MeasurementsInsertBatch` → `public.sp_insert_measurement_batch`
- `Plant.MeasurementsInsertBatchPrc` → `public.sp_insert_measurement_batch_prc`

## Особенности
- В `central_ft.measurement_batches` **не** включаем `id/created_at` (иначе FDW может прислать `NULL`).
- Триггер central `trg_measurement_batches_bi` обновляет `assets_global` и `analytics_cr` при вставках батчей.
- Инвентаризация (`DatabaseInventoryInspector`) может автоматически применять SQL по профилю.
