# Архитектура системы

## Общая картина
- Центральная БД (PostgreSQL): справочник активов, политика риска, журнал батчей замеров, расчёты CR/риска, агрегации.
- Заводы ANPZ/KRNPZ (PostgreSQL): локальные таблицы измерений и триггеры; батчи замеров пишутся напрямую в central через FDW (`central_ft.measurement_batches`).
- .NET-ядро: `OilErp.Core` (контракты/DTO/сервисы), `OilErp.Infrastructure` (StorageAdapter, bootstrap/inventory, конфиг).
- Клиентские поверхности: CLI/смоук-раннер (`OilErp.Tests.Runner`), Avalonia UI (`OilErp.Ui`).
- Поток данных: заводская процедура → FDW вставка в `central.measurement_batches` → триггер central обновляет `analytics_cr`/`assets_global` → аналитические `fn_*`.

## Потоки данных
1) **Сбор измерений на заводе**  
   `sp_insert_measurement_batch_prc` (ANPZ/KRNPZ): валидирует точки (label/ts/thickness>0), пишет в локальные таблицы, публикует агрегированный батч (prev/last) в `central_ft.measurement_batches`.

2) **Central: батчи и аналитика**  
   `measurement_batches` — журнал батчей замеров (вставка через FDW).  
   Триггер `trg_measurement_batches_bi` гарантирует наличие записи в `assets_global` и обновляет `analytics_cr` (через `fn_calc_cr`).

3) **Аналитика**  
   - `fn_top_assets_by_cr` — топ по CR из `analytics_cr`.  
   - `fn_asset_summary_json` — JSON сводка (asset + analytics + risk).  
   - `fn_eval_risk` — уровень риска по политике.  
   - `fn_plant_cr_stats` — mean/P90/count по заводу.

4) **Политики/активы**  
   `fn/sp_asset_upsert`, `fn/sp_policy_upsert` — поддержка справочников; функции делегируют в процедуры с OUT id.

## Компоненты
- **Core**: `IStoragePort`, `IStorageTransaction` (сейвпоинты), DTO для топ/риск/plant stats, сервисы-обёртки над SQL, нормализация ввода в `AppServiceBase`.
- **Infrastructure**: `StorageAdapter` (Npgsql, кэш pg_proc метаданных), `StorageConfigProvider`, общий `AppLogger`/`DatabaseBootstrapper`/`DatabaseInventoryInspector`.
- **Tests/CLI**: смоук-сценарии для подключения, аналитики и FDW; CLI команды `add-asset/policy`, `add-measurements-anpz` (JSON/CSV), `summary/top-by-cr/eval-risk/plant-cr`.
- **UI**: Avalonia, `KernelGateway` использует общий bootstrapper/config, `MeasurementDataProvider` асинхронный, панели Analytics/Measurements используют те же сервисы.

## Применяемые паттерны
- Порт/адаптер (StoragePort ↔ StorageAdapter).
- FDW + прямые вставки + триггер в central для межбазового обмена.
- MVVM в UI, DTO-first в Core.
- Идемпотентные SQL-скрипты (OR REPLACE/IF NOT EXISTS).
