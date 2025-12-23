# Operation Name to SQL Mapping

Карта соответствий между константами `OperationNames` и SQL‑объектами. Значение каждой константы — fully‑qualified имя `schema.name`.

| OperationName | SQL | Kind | Returns | Params |
|---|---|---|---|---|
| `Plant.MeasurementsInsertBatch` | `public.sp_insert_measurement_batch` | Command | `integer` | `p_asset_code text, p_points jsonb, p_source_plant text` (валидация/нормализация, делегирует в prc) |
| `Plant.MeasurementsInsertBatchPrc` | `public.sp_insert_measurement_batch_prc` | Command | `OUT p_inserted integer` | `p_asset_code text, p_points jsonb, p_source_plant text` |
| `Central.CalcCr` | `public.fn_calc_cr` | Query | `numeric` | `prev_thk numeric, prev_date timestamptz, last_thk numeric, last_date timestamptz` |
| `Central.AssetUpsert` | `public.fn_asset_upsert` | Command | `bigint` | `p_asset_code text, p_name text DEFAULT NULL, p_type text DEFAULT NULL, p_plant_code text DEFAULT NULL` |
| `Central.PolicyUpsert` | `public.fn_policy_upsert` | Command | `bigint` | `p_name text, p_low numeric, p_med numeric, p_high numeric` |
| `Central.EvalRisk` | `public.fn_eval_risk` | Query | `SETOF` | `p_asset_code text, p_policy_name text DEFAULT 'default'` |
| `Central.AnalyticsAssetSummary` | `public.fn_asset_summary_json` | Query | `jsonb` | `p_asset_code text, p_policy_name text DEFAULT 'default'` |
| `Central.AnalyticsTopAssetsByCr` | `public.fn_top_assets_by_cr` | Query | `SETOF` | `p_limit int DEFAULT 50` |
| `Central.AnalyticsPlantCrStats` | `public.fn_plant_cr_stats` | Query | `SETOF` | `p_plant text, p_from timestamptz, p_to timestamptz` |
| `Central.SpPolicyUpsert` | `public.sp_policy_upsert` | Command | `OUT p_id bigint` | `p_name text, p_low numeric, p_med numeric, p_high numeric` |
| `Central.SpAssetUpsert` | `public.sp_asset_upsert` | Command | `OUT p_id bigint` | `p_asset_code text, p_name text, p_type text, p_plant_code text` |

Примечания
- `Kind`: функции, возвращающие набор/JSON, считаются Query; процедуры и функции, возвращающие скаляры для мутаций, идут как Command.
- Вставки в `public.measurement_batches` выполняются напрямую (FDW с заводов или insert из UI), поэтому они не отображаются в `OperationNames`.
- Триггеры (`trg_measurement_batches_bi`, `trg_measurements_ai`) и их функции — инфраструктурные объекты, не имеют прямых оберток в Core.
