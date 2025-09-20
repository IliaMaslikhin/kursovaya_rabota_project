# ERP Distributed SQL Pack

Дата сборки: 20250918_1822

## Структура
- `central/`
  - `01_tables.sql` — таблицы и индексы: assets_global, risk_policies, analytics_cr, events_inbox.
  - `02_functions_core.sql` — функции: fn_calc_cr, fn_asset_upsert, fn_policy_upsert, fn_events_enqueue, fn_events_peek, fn_ingest_events, fn_events_requeue, fn_events_cleanup, fn_eval_risk, fn_asset_summary_json, fn_top_assets_by_cr.
  - `03_procedures.sql` — процедурные обёртки: sp_ingest_events(p_limit), sp_events_enqueue(...), sp_events_requeue(...), sp_events_cleanup(...), sp_policy_upsert(...), sp_asset_upsert(...).
  - `04_function_sp_ingest_events_legacy.sql` — ранняя версия обработки очереди как функция sp_ingest_events() RETURNS void (оставлена для совместимости).

- `anpz/`
  - `01_tables.sql` — assets_local, measurement_points, measurements, local_events + индексы.
  - `02_fdw.sql` — postgres_fdw, central_srv, user mapping (с плейсхолдерами), внешняя таблица central_ft.events_inbox.
  - `03_trigger_measurements_ai.sql` — триггер и функция на INSERT в measurements → запись в local_events.
  - `04_function_sp_insert_measurement_batch.sql` — батч-вставка измерений и публикация сводки в центральный inbox (через FDW), вставка id/created_at через DEFAULT.
  - `05_procedure_wrapper.sql` — процедурная обёртка для CALL.

- `krnpz/`
  - Аналогично `anpz/`, только p_source_plant по умолчанию "KRNPZ".

## Порядок применения
1) central: 01 → 02 → 03 → (опционально 04).
2) anpz:   01 → 02 → 03 → 04 → 05.
3) krnpz:  01 → 02 → 03 → 04 → 05.

## Что создано (итог)
- База central: 4 таблицы, 10 функций, 6 процедур, индексы.
- Базы anpz/krnpz: по 4 таблицы, 1 триггер + функция триггера, 1 функция, 1 процедура, FDW сервер + user mapping + внешняя таблица.

## Примечания
- В FDW (`02_fdw.sql`) оставлены плейсхолдеры для логина/пароля центра. Если нужен явный логин — раскомментируй блок CREATE USER MAPPING и подставь креды.
- Все DDL идемпотентны (IF NOT EXISTS / OR REPLACE). Скрипты можно гонять повторно.
