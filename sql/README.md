# ERP Distributed SQL Pack (актуальный)

Дата сборки: 20251130

## Структура и содержимое

### central/
- `01_tables.sql` — таблицы и индексы:
  - `public.assets_global` (активы), `public.risk_policies` (политики риска), `public.analytics_cr` (расчёт CR), `public.events_inbox` (очередь событий).
- `02_functions_core.sql` — функции:
  - `fn_calc_cr(prev_thk, prev_date, last_thk, last_date)` — расчёт скорости коррозии (CR).
  - `fn_asset_upsert(p_asset_code, p_name, p_type, p_plant_code)` — upsert актива.
  - `fn_policy_upsert(p_name, p_low, p_med, p_high)` — upsert политики риска.
  - `fn_events_enqueue(p_event_type, p_source_plant, p_payload)` — положить событие в central.inbox.
  - `fn_events_peek(p_limit)` — выбрать непросессed события.
  - `fn_ingest_events(p_limit)` — разобрать очередь: парсит payload, пишет в analytics_cr, ставит processed_at.
  - `fn_events_requeue(p_ids bigint[])` — вернуть события в очередь (processed_at NULL).
  - `fn_events_cleanup(p_older_than interval)` — удалить старые processed события.
  - `fn_eval_risk(p_asset_code, p_policy_name)` — расчёт риска по политике.
  - `fn_asset_summary_json(p_asset_code, p_policy_name)` — JSON-сводка (asset/analytics/risk).
  - `fn_top_assets_by_cr(p_limit)` — топ CR из analytics_cr.
  - `fn_plant_cr_stats(p_plant, p_from, p_to)` — агрегаты CR по заводу (mean, P90, count).
- `03_procedures.sql` — процедурные обёртки:
  - `sp_ingest_events(p_limit)`, `sp_events_enqueue(...)`, `sp_events_requeue(...)`, `sp_events_cleanup(...)`, `sp_policy_upsert(...)`, `sp_asset_upsert(...)`.
- `04_function_sp_ingest_events_legacy.sql` — совместимость: прежний вариант sp_ingest_events() как FUNCTION.

### anpz/
- `01_tables.sql` — `assets_local`, `measurement_points`, `measurements`, `local_events` (+ индексы).
- `02_fdw.sql` — `postgres_fdw`, `central_srv`, user mapping (плейсхолдеры), внешняя таблица `central_ft.events_inbox`.
- `03_trigger_measurements_ai.sql` — триггер AFTER INSERT на measurements → запись в local_events.
- `04_function_sp_insert_measurement_batch.sql` — батч-вставка измерений, публикация сводки в central inbox через FDW.
- `05_procedure_wrapper.sql` — CALL-обёртка над batch-функцией.

### krnpz/
- Аналогично `anpz/`, p_source_plant по умолчанию "KRNPZ".

## Порядок применения
1) central: 01 → 02 → 03 → (опционально 04).
2) anpz:   01 → 02 → 03 → 04 → 05.
3) krnpz:  01 → 02 → 03 → 04 → 05.

## Итоговый состав
- Central: 4 таблицы, 13 функций, 6 процедур, индексы.
- ANPZ/KRNPZ: по 4 таблицы, 1 триггер + функция триггера, 1 функция, 1 процедура, FDW сервер + user mapping + внешняя таблица.

## Логика взаимодействия
- Вставка/обновление справочников: `fn_asset_upsert`, `fn_policy_upsert` (обёртки `sp_*`).
- Очередь событий: `fn_events_enqueue` кладёт JSON; `fn_ingest_events` парсит payload (asset_code, prev/last_thk/date) → пишет в `analytics_cr`, ставит processed_at; `fn_events_peek`/`fn_events_requeue`/`fn_events_cleanup` обслуживают очередь.
- Аналитика: `fn_calc_cr` используется внутри ingest; `fn_top_assets_by_cr` читает CR; `fn_asset_summary_json` объединяет asset+analytics+risk; `fn_eval_risk` возвращает риск-таблицу; `fn_plant_cr_stats` агрегирует CR по заводу (mean/P90/count).
- Заводы: INSERT в `measurements` вызывает триггер → `local_events`; `sp_insert_measurement_batch` вставляет батч точек, публикует payload в central_inbox через FDW.

## Кейсы использования (реализованные)
- CLI/UI add-asset/policy: `fn_asset_upsert`, `fn_policy_upsert` (`sp_*` через обёртки).
- CLI/UI events enqueue/ingest/watch: `fn_events_enqueue`, `fn_ingest_events`, `fn_events_peek`, `fn_events_requeue`, `fn_events_cleanup`, LISTEN/NOTIFY через адаптер.
- CLI/UI аналитика: `fn_top_assets_by_cr`, `fn_asset_summary_json`, `fn_eval_risk`, `fn_plant_cr_stats`.
- CLI add-measurements-anpz/UI форма замеров: `sp_insert_measurement_batch` (ANPZ/KRNPZ) → FDW `central_ft.events_inbox` → `fn_ingest_events`.

## Потенциальные кейсы без новых SQL
- Автопроверка профилей: прогон `fn_plant_cr_stats` по окну дат; проверка FDW доступности через `central_ft.events_inbox`.
- Ручной requeue/cleanup с параметрами для обслуживания очереди (уже есть функции/процедуры).
- Агрегации CR/риск в UI/отчётах через существующие `fn_*` без добавления SQL.

## Примечания
- В FDW (`02_fdw.sql`) плейсхолдеры логина/пароля центра — при необходимости раскомментировать `CREATE USER MAPPING` и подставить креды.
- Все скрипты идемпотентны (IF NOT EXISTS/OR REPLACE) и могут применяться повторно.
