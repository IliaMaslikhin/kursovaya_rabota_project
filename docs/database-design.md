# Дизайн базы данных

## 1. Общая модель
Система использует три базы PostgreSQL:
- `central` — главный узел (справочники, батчи, аналитика).
- `anpz`, `krnpz` — заводские узлы (локальные измерения + отправка батчей в central через FDW).

Основной поток: заводские процедуры → FDW `central_ft.measurement_batches` → триггер central → `assets_global` + `analytics_cr`.

## 2. Central (schema public)

### Таблицы
- `assets_global`
  - `asset_code` UNIQUE, `name`, `type`, `plant_code`, `created_at`.
  - Используется как справочник активов для всех заводов.
- `risk_policies`
  - `name` UNIQUE, пороги `threshold_low/med/high`.
- `analytics_cr`
  - `asset_code` (FK на `assets_global`), prev/last значения, `cr`, `updated_at`.
  - Индекс `ux_analytics_cr_asset_code` обеспечивает один snapshot на актив.
- `measurement_batches`
  - Журнал батчей с заводов и central.
  - Важные поля: `source_plant`, `asset_code`, `prev_*`, `last_*`, `last_label`, `last_note`, `created_at`.
  - CHECK‑ограничения: непустой `asset_code`, `last_thk > 0`, `prev_date <= last_date`, `prev_thk >= last_thk`.

### Функции (central)
- `fn_calc_cr(prev_thk, prev_date, last_thk, last_date)` — расчет скорости коррозии (IMMUTABLE SQL‑функция).
- `fn_asset_upsert(...)` → делегирует в `sp_asset_upsert`, возвращает id.
- `fn_policy_upsert(...)` → делегирует в `sp_policy_upsert`, возвращает id.
- `fn_eval_risk(asset_code, policy)` — оценивает риск и возвращает таблицу (level + thresholds).
- `fn_asset_summary_json(asset_code, policy)` — JSON‑сводка (asset/analytics/risk).
- `fn_top_assets_by_cr(limit)` — топ активов по CR.
- `fn_plant_cr_stats(plant, from, to)` — среднее и P90 по CR.

### Процедуры и триггеры (central)
- `sp_asset_upsert` / `sp_policy_upsert` — процедуры с OUT‑параметрами для апсерта справочников.
- `trg_measurement_batches_bi_fn` + триггер `trg_measurement_batches_bi`:
  - При вставке в `measurement_batches` гарантирует наличие `assets_global`.
  - Делает upsert `analytics_cr` и считает `cr` через `fn_calc_cr`.
- В `03_procedures.sql` удалена старая очередь `events_inbox` (legacy сценарии).

## 3. Заводы ANPZ / KRNPZ (schema public)

### Таблицы
- `assets_local` — локальный справочник активов.
- `measurement_points` — точки измерений (UNIQUE: asset_id+label).
- `measurements` — фактические замеры (CHECK: `thickness > 0`).
- `local_events` — журнал локальных событий (используется триггером `trg_measurements_ai`).

### Триггеры
- `trg_measurements_ai_fn` (AFTER INSERT ON `measurements`) — пишет событие в `local_events`.

### Функция и процедура ingest
- `sp_insert_measurement_batch(p_asset_code, p_points, p_source_plant)`
  - Валидирует JSON‑массив.
  - Читает поля как `x.value->>'field'` (jsonb_array_elements).
  - Сортирует, нормализует, вызывает `sp_insert_measurement_batch_prc`.
- `sp_insert_measurement_batch_prc(...)`
  - Вставляет актив/точки/замеры в локальные таблицы.
  - Проверяет монотонность дат и не‑возрастание толщины.
  - Формирует агрегированный батч (prev/last) и вставляет в `central_ft.measurement_batches`.

## 4. FDW (postgres_fdw)
FDW используется для записи в central напрямую, без очереди и без интеграционного сервиса.

- `CREATE EXTENSION postgres_fdw`.
- Сервер `central_srv` хранит host/port/dbname центральной БД.
- Схема `central_ft` содержит внешнюю таблицу `measurement_batches`.
- Внешняя таблица **не включает `id` и `created_at`**, иначе `postgres_fdw` может отправить `NULL` в identity‑колонки.
- `DatabaseInventoryInspector` при автоприменении `02_fdw.sql` корректирует параметры `central_srv` и user mapping по строкам окружения (`OILERP__DB__CONN` / `OIL_ERP_PG`).

## 5. Почему функции и процедуры разделены
- **Процедуры** (`sp_*`) выполняют мутации и возвращают OUT‑параметры (id/inserted).
- **Функции‑обертки** (`fn_*`) удобны для `SELECT` и прямого вызова через `IStoragePort`.
- **Аналитика** реализована как `fn_*` для удобства чтения и агрегации.

## 6. Порядок применения SQL
- **central**: `01_tables.sql` → `02_functions_core.sql` → `03_procedures.sql`.
- **anpz / krnpz**: `01_tables.sql` → `02_fdw.sql` → `03_trigger_measurements_ai.sql` → `04_function_sp_insert_measurement_batch.sql` → `05_procedure_wrapper.sql`.
- Нагрузочные сценарии: `central/99_mass_test_events.sql` (ручной запуск).

## 7. Итоговый поток данных
1) Заводская функция принимает JSON‑массив точек.
2) Процедура пишет локальные измерения и отправляет батч в central через FDW.
3) Триггер central делает upsert `analytics_cr` и связывает активы.
4) UI строит аналитические отчёты из `analytics_cr`.
