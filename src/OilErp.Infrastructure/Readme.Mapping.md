# Operation Name to SQL Object Mapping

This document maps C# operation names to their corresponding SQL functions and procedures.

| Operation Name | SQL Object | File Path |
|---|---|---|
| `plant.measurements.insert_batch` | `sp_insert_measurement_batch(p_asset_code TEXT, p_points JSONB, p_source_plant TEXT)` | `sql/anpz/04_function_sp_insert_measurement_batch.sql` (lines 1-84)<br/>`sql/krnpz/04_function_sp_insert_measurement_batch.sql` (lines 1-84) |
| `central.events.ingest` | `fn_ingest_events(p_limit int DEFAULT 1000)` | `sql/central/02_functions_core.sql` (lines 93-156) |
| `central.events.cleanup` | `fn_events_cleanup(p_older_than interval DEFAULT '30 days')` | `sql/central/02_functions_core.sql` (lines 171-182) |
| `central.analytics.asset_summary` | `fn_asset_summary_json(p_asset_code text, p_policy_name text DEFAULT 'default')` | `sql/central/02_functions_core.sql` (lines 223-253) |
| `central.analytics.top_assets_by_cr` | `fn_top_assets_by_cr(p_limit int DEFAULT 50)` | `sql/central/02_functions_core.sql` (lines 256-265) |
