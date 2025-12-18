# Operation Name to SQL Mapping

Ниже приведена карта соответствий между C# константами `OperationNames` и реальными SQL-операциями. Значение каждой константы — fully-qualified имя `schema.name`.

| OperationName | SQL | Kind | Returns | Params |
|---|---|---|---|---|
| `Plant.MeasurementsInsertBatch` | `public.sp_insert_measurement_batch` | Command | SCALAR | `p_asset_code text, p_points jsonb, p_source_plant text` (валидация/нормализация, делегирует в prc) |
| `Plant.MeasurementsInsertBatchPrc` | `public.sp_insert_measurement_batch_prc` | Command | SCALAR (OUT) | `p_asset_code text, p_points jsonb, p_source_plant text, OUT p_inserted integer` |
| `Central.CalcCr` | `public.fn_calc_cr` | Query | SCALAR | `prev_thk numeric, prev_date timestamptz, last_thk numeric, last_date timestamptz` |
| `Central.AssetUpsert` | `public.fn_asset_upsert` | Command | SCALAR | `p_asset_code text, p_name text DEFAULT NULL, p_type text DEFAULT NULL, p_plant_code text DEFAULT NULL` (delegирует в `sp_asset_upsert`) |
| `Central.PolicyUpsert` | `public.fn_policy_upsert` | Command | SCALAR | `p_name text, p_low numeric, p_med numeric, p_high numeric` (delegирует в `sp_policy_upsert`) |
| `Central.EvalRisk` | `public.fn_eval_risk` | Query | SET | `p_asset_code text, p_policy_name text DEFAULT 'default'` |
| `Central.AnalyticsAssetSummary` | `public.fn_asset_summary_json` | Query | JSON | `p_asset_code text, p_policy_name text DEFAULT 'default'` |
| `Central.AnalyticsTopAssetsByCr` | `public.fn_top_assets_by_cr` | Query | SET | `p_limit int DEFAULT 50` |
| `Central.AnalyticsPlantCrStats` | `public.fn_plant_cr_stats` | Query | SET | `p_plant text, p_from timestamptz, p_to timestamptz` |
| `Central.SpPolicyUpsert` | `public.sp_policy_upsert` | Command | SCALAR (OUT) | `p_name text, p_low numeric, p_med numeric, p_high numeric, OUT p_id bigint` |
| `Central.SpAssetUpsert` | `public.sp_asset_upsert` | Command | SCALAR (OUT) | `p_asset_code text, p_name text, p_type text, p_plant_code text, OUT p_id bigint` |

Примечания
- Kind определяется так: RETURNS SET/табличный → Query; RETURNS JSON/JSONB → Query; PROCEDURE/RETURNS void → Command; прочие скалярные возвращаемые типы помечены как SCALAR (доп. категория для ясности).
- Поток завод → central идёт через FDW-вставку в `public.measurement_batches` (таблица), поэтому этой операции нет в `OperationNames`.
