# Итоги ревью (грязь и незакрытые фичи)

## Ключевые несоответствия замыслу
- Многобазовая схема (central + ANPZ/KRNPZ с outbox/inbox через FDW) не реализована: в коде везде один `IStoragePort`/одна строка подключения, нет маршрутизации на заводские БД и нет работы с FDW очередями. Обёртки для заводов вызываются тем же соединением, что и центральные функции, поэтому `sp_insert_measurement_batch` фактически недостижим при стандартной конфигурации.
- SQL-схема сильно урезана относительно `docs/database-design.md`: из каталогов/рисков/инцидентов/трендов есть только `assets_global`, `risk_policies`, `analytics_cr`, `events_inbox` и минимальные таблицы заводов; остальная модель (materials/coatings/fluids/incidents/etc.) отсутствует в `sql/central/*.sql` и `sql/anpz|krnpz/*.sql`.
- Поток данных «замер → local_events → FDW → central inbox → ingest» не упражняется ни CLI, ни тестами, ни UI: посев и команды пишут прямо в центральный inbox через `fn_events_enqueue`/`fn_ingest_events`, обходя триггеры и внешние таблицы заводов.

## Core / Infrastructure
- Бизнес-логика утекает из БД: `src/OilErp.Core/Services/Aggregations/PlantCrService.cs` собирает до 100k строк, делает N+1 вызовы `fn_asset_summary_json` и сам считает среднее/P90 вместо SQL-агрегаций; это и медленно, и противоречит идее «всё считает БД».
- Дубли сервисов и неиспользуемые обёртки: `AnalyticsService` дублирует `FnAssetSummaryJsonService`; заводские сервисы есть для ANPZ/KRNPZ, но кроме ANPZ-CLI их никто не зовёт.
- Контракт `IStoragePort` урезан: нет методов LISTEN/UNLISTEN и явных Commit/Rollback. Из-за этого `Program.cs` использует reflection, а CLI `watch` кастит к `StorageAdapter`. Диагностику через notifications в UI реализовать нечем.
- Прослойка нарушает слои: `StorageAdapter` зависит от `AppLogger` из тестового проекта (линк в `src/OilErp.Infrastructure/OilErp.Infrastructure.csproj`), то же в UI csproj — runtime сборки тянут код тестов.
- Конфигурация разъезжается: `StorageAdapter` ищет `OIL_ERP_PG`, CLI/tests используют `OILERP__DB__CONN`, UI требует ручной ввод строки — единого источника правды нет.
- Функции/таблицы не проверяются/создаются в заводах: `DatabaseBootstrapper` создаёт базы anpz/krnpz, но `DatabaseInventoryInspector` применяет SQL только к текущей базе, поэтому схемы заводов остаются пустыми.

## UI
- Обещанный MCP/операторский интерфейс отсутствует: `src/OilErp.Ui/Views/MainWindow.axaml` показывает лишь подключение и голые upsert/enqueue/ингест; нет панелей измерений, риск-статусов, диагностики или MCP-секций.
- Мёртвый код: `MeasurementDataProvider`, `MeasurementSnapshotService`, `AddMeasurementFormViewModel`, `MeasurementIngestionService`, `McpSectionViewModel`, `StatusPulseViewModel`, `DiagnosticEntryViewModel` нигде не используются; `KernelGateway.IsLive`/`BootstrapInfo` не выводятся.
- Инжест замеров поломан: `src/OilErp.Ui/Services/MeasurementIngestionService.cs` возвращает `Success=true` даже в catch, не обновляет аналитику и не поддерживает батчи/транзакции. Нет выбора БД завода — всё уходит в одно соединение.
- Аналитика отображает неверный завод: `fn_top_assets_by_cr` не возвращает `plant_code`, а `src/OilErp.Ui/ViewModels/AnalyticsPanelViewModel.cs` подставляет "CENTRAL". Риск-уровни берутся отдельным вызовом без кеша, что даёт лишние запросы.
- UI игнорирует единые настройки: нет чтения `TestEnvironment.LoadStorageConfig()`/env-переменных, поэтому UI/CLI могут смотреть в разные базы; офлайн/InMemory-режим не подключён.

## CLI и тесты
- CLI завязан на одну БД: `add-measurements-anpz` вызывает `SpInsertMeasurementBatchService`, но соединение — то же, что и для central; KRNPZ-команды нет, real заводской путь не проверяется.
- Smoke-тесты проверяют только центральные функции. `StorageSmoke` и `ExtendedSmokeTests` дважды сеют одну и ту же выборку через `fn_events_enqueue`, не трогая триггер `trg_measurements_ai` и FDW. Нет тестов на LISTEN/NOTIFY, на UI-сервисы, на раздельные профили БД.
- Низкополезные тесты: `ValidationSmokeTests.TestMissingObjectReminderFormatting` лишь проверяет наличие "TODO" в строке; `AsyncSmokeTests` гоняют `pg_sleep`, но не подтверждают корректность реальных таймаутов/откатов транзакций.

