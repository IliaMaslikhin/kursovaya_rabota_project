# Архитектура системы

## Общая картина
- Центральная БД (PostgreSQL): справочник активов, политика риска, очередь событий, расчёты CR/ризка, агрегации.
- Заводы ANPZ/KRNPZ (PostgreSQL): локальные таблицы измерений и триггеры, публикующие события в центральную очередь через FDW (`central_ft.events_inbox`).
- .NET-ядро: `OilErp.Core` (контракты/DTO/сервисы), `OilErp.Infrastructure` (StorageAdapter, bootstrap/inventory, конфиг).
- Клиентские поверхности: CLI/смоук-раннер (`OilErp.Tests.Runner`), Avalonia UI (`OilErp.Ui`).
- Поток данных: заводская процедура → FDW inbox central → `fn/sp_ingest_events` → `analytics_cr`/`assets_global` → аналитические `fn_*`.

## Потоки данных
1) **Сбор измерений на заводе**  
   `sp_insert_measurement_batch_prc` (ANPZ/KRNPZ): валидирует точки (label/ts/thickness>0), пишет в локальные таблицы, публикует событие `HC_MEASUREMENT_BATCH` в `central_ft.events_inbox`.

2) **Центральная очередь**  
   `fn_events_enqueue` кладёт произвольные события, `fn_events_peek/requeue/cleanup` обслуживают очередь.  
   `fn_ingest_events` (делегирует в `sp_ingest_events`) читает только `HC_MEASUREMENT_BATCH` с валидными датами/толщинами, считает CR через `fn_calc_cr`, обновляет `analytics_cr`, проставляет `processed_at`, шлёт `NOTIFY events_ingest`.

3) **Аналитика**  
   - `fn_top_assets_by_cr` — топ по CR из `analytics_cr`.  
   - `fn_asset_summary_json` — JSON сводка (asset + analytics + risk).  
   - `fn_eval_risk` — уровень риска по политике.  
   - `fn_plant_cr_stats` — mean/P90/count по заводу.

4) **Политики/активы**  
   `fn/sp_asset_upsert`, `fn/sp_policy_upsert` — поддержка справочников; функции делегируют в процедуры с OUT id.

## Компоненты
- **Core**: `IStoragePort`, `IStorageTransaction` (сейвпоинты), DTO для топ/риск/peek/plant stats, сервисы-обёртки над SQL, нормализация ввода в `AppServiceBase`.
- **Infrastructure**: `StorageAdapter` (Npgsql, кэш pg_proc метаданных, LISTEN/NOTIFY), `StorageConfigProvider`, общий `AppLogger`/`DatabaseBootstrapper`/`DatabaseInventoryInspector`.
- **Tests/CLI**: смоук-сценарии для подключения, очереди, аналитики, FDW, LISTEN, негативные/нагрузочные кейсы; CLI команды `add-asset/policy`, `add-measurements-anpz` (JSON/CSV), `events-*`, `summary/top-by-cr/eval-risk/plant-cr`, `watch`.
- **UI**: Avalonia, `KernelGateway` использует общий bootstrapper/config, `MeasurementDataProvider` асинхронный, панели Analytics/Measurements/Diagnostics используют те же сервисы, подписки LISTEN через StorageAdapter.

## Применяемые паттерны
- Порт/адаптер (StoragePort ↔ StorageAdapter).
- FDW + Outbox/Inbox для межбазового обмена.
- MVVM в UI, DTO-first в Core.
- Идемпотентные SQL-скрипты (OR REPLACE/IF NOT EXISTS).
