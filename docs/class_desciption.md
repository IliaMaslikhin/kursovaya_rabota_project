# Описание классов и их взаимодействий

## OilErp.Core
- **Abstractions/DbClientBase** — базовый класс для клиентов БД, реализует `IStoragePort` и требует реализации `ExecuteQueryAsync<T>`, `ExecuteCommandAsync`, `BeginTransactionAsync`, `SubscribeAsync`, `UnsubscribeAsync`, события `Notified`.
- **Abstractions/AppServiceBase** — базовый сервис, хранящий `IStoragePort` и проверяющий, что он не null.
- **Contracts/IStoragePort** — контракт доступа к БД: `ExecuteQueryAsync<T>(QuerySpec)`, `ExecuteCommandAsync(CommandSpec)`, `BeginTransactionAsync()`, подписка/отписка на LISTEN и событие `Notified`.
- **Contracts/IStorageTransaction** — асинхронная транзакция с `CommitAsync` и `RollbackAsync`.
- **Contracts/ICoreKernel** — ядро, отдаёт `IStoragePort` через свойство `Storage`.
- **Dto/CommandSpec, QuerySpec** — описание вызова функции/процедуры с именем операции, параметрами и опциональным тайм-аутом.
- **Dto/DatabaseProfile** — enum профилей (Central, PlantAnpz, PlantKrnpz, Unknown).
- **Dto/DbNotification** — данные LISTEN/NOTIFY (канал, полезная нагрузка, PID).
- **Dto/MeasurementPointDto** — точка замера (метка, время, толщина, комментарий).
- **Dto/OperationResult<T>** — результат операции с данными, ошибками и метаданными.
- **Operations/OperationNames** — константы имён SQL-функций/процедур для центральной БД и заводов.
- **Services/Dtos/AssetSummaryDto** — десериализуемая модель JSON из `fn_asset_summary_json` (asset/analytics/risk).
- **Services/Dtos/PlantCrAggregateDto** — агрегированная статистика CR по заводу за период.
- **Services/Dtos/TopAssetCrDto** — элемент выдачи топ-CR.

### Сервисы центральной БД (обёртки вокруг SQL)
Все наследуют `AppServiceBase`, собирают `QuerySpec` или `CommandSpec` и вызывают порт:
- **FnAssetSummaryJsonService** — `fn_asset_summary_jsonAsync(asset_code, policy)` возвращает `AssetSummaryDto?`.
- **FnTopAssetsByCrService** — `fn_top_assets_by_crAsync(limit)` → список словарей с полями SQL.
- **FnEvalRiskService** — `fn_eval_riskAsync(asset_code, policy)` → список словарей.
- **FnCalcCrService** — `fn_calc_crAsync(prev_thk, prev_date, last_thk, last_date)` → список словарей/значений.
- **FnPlantCrStatsService** — `fn_plant_cr_statsAsync(plant, from, to)` → список словарей со статистикой.
- **FnEventsEnqueueService** — `fn_events_enqueueAsync(event_type, source_plant, payload)` → затронутые строки.
- **FnEventsPeekService** — `fn_events_peekAsync(limit)` → выдача очереди.
- **FnIngestEventsService** — `fn_ingest_eventsAsync(limit)` → количество обработанных событий.
- **FnEventsRequeueService** — `fn_events_requeueAsync(ids[])` → количество перезаписанных.
- **FnEventsCleanupService** — `fn_events_cleanupAsync(interval)` → очищенные записи.
- **FnAssetUpsertService** — `fn_asset_upsertAsync(asset_code, name, type, plant_code)` → затронутые строки.
- **FnPolicyUpsertService** — `fn_policy_upsertAsync(name, low, med, high)` → затронутые строки.
- **SpIngestEventsService** — `sp_ingest_eventsAsync(limit)` — процедурная версия ingestion.
- **SpEventsEnqueueService** — `sp_events_enqueueAsync(...)` — процедурная версия enqueue.
- **SpEventsRequeueService** — `sp_events_requeueAsync(ids[])`.
- **SpEventsCleanupService** — `sp_events_cleanupAsync(interval)`.
- **SpAssetUpsertService** — `sp_asset_upsertAsync(...)`.
- **SpPolicyUpsertService** — `sp_policy_upsertAsync(...)`.

### Заводские сервисы
- **Plants/ANPZ/SpInsertMeasurementBatchService** — `sp_insert_measurement_batchAsync(asset_code, points_json, source_plant)`.
- **Plants/ANPZ/SpInsertMeasurementBatchPrcService** — процедурный вызов `sp_insert_measurement_batch_prcAsync(...)`.
- **Plants/KRNPZ/SpInsertMeasurementBatchService** — то же для KRNPZ.
- **Plants/KRNPZ/SpInsertMeasurementBatchPrcService** — процедурная версия для KRNPZ.

### Агрегации
- **Aggregations/PlantCrService** — использует `FnPlantCrStatsService`, читает cr_mean/cr_p90/assets_count, возвращает `PlantCrAggregateDto`.

## OilErp.Infrastructure
- **Adapters/StorageAdapter** — реализация `IStoragePort` на Npgsql. `ExecuteQueryAsync` и `ExecuteCommandAsync` получают метаданные функции/процедуры из каталога, формируют вызов с параметрами, различают JSON/SETOF/скаляры, логируют через `AppLogger`. `BeginTransactionAsync` создаёт соединение+транзакцию, хранит их в `AsyncLocal` для повторных вызовов. `SubscribeAsync`/`UnsubscribeAsync` управляют отдельным LISTEN-соединением и поднимают событие `Notified`. Внутренний `PgTransactionScope` реализует `IStorageTransaction` с `CommitAsync`, `RollbackAsync`, а также методами сейвпоинтов (Create/RollbackTo/Release) сверх интерфейса.
- **Adapters/KernelAdapter** — обёртка над `IStoragePort`, реализует `ICoreKernel.Storage`.
- **Config/StorageConfig** — record с `ConnectionString` и тайм-аутом.
- **Logging/AppLogger** — минимальный логгер (консоль + файл в %APPDATA%/OilErp/logs).

## OilErp.Ui
### Запуск/Views
- **Program** — точка входа Avalonia, ловит фатальные ошибки.
- **App** — создаёт `MainWindow`, отключает встроенную валидацию Avalonia.
- **ViewLocator** — мапит ViewModel → View через отражение.
- **Views/MainWindow** — код-бихайнд с вызовом `InitializeComponent`.

### Сервисы и инфраструктура UI
- **Services/AppLogger** — копия минимального логгера для UI.
- **Services/FirstRunTracker** — хранит маркер первого запуска в %APPDATA%/OilErp/first-run.machine.
- **Services/DatabaseBootstrapper** — создаёт базы central/anpz/krnpz при необходимости, проверяет схему через `DatabaseInventoryInspector`, копирует гайд на рабочий стол при ошибке, возвращает `BootstrapResult`.
- **Services/DatabaseInventoryInspector** — читает функции/процедуры/таблицы/триггеры из БД, сверяет с ожидаемыми объектами для профиля, при необходимости выполняет SQL-скрипты из `sql/` (по профилю), умеет печатать итог.
- **Services/KernelGateway** — статический `Create(conn)`: запускает `DatabaseBootstrapper`, проверяет `fn_calc_cr`, создаёт `StorageAdapter`, выставляет флаги `IsLive`, `StatusMessage`, `StorageFactory`.
- **Services/StoragePortFactory** — возвращает центральный порт или новый `StorageAdapter` по заводскому профилю из переменных окружения (ANPZ/KRNPZ), читает тайм-аут.
- **Services/MeasurementSnapshotService** — читает `*_measurements.json` из каталога Data, превращает в `MeasurementSeries`.
- **Services/MeasurementDataProvider** — пытается загрузить данные из БД (`FnTopAssetsByCrService`, `FnAssetSummaryJsonService`), при пустом результате добавляет снапшоты. Возвращает `MeasurementDataResult` с сериями и статусом.
- **Services/MeasurementIngestionService** — собирает JSON по `MeasurementPointDto`, выбирает порт через `StoragePortFactory`, оборачивает вызов в транзакцию и вызывает `SpInsertMeasurementBatchService` (ANPZ/KRNPZ). Возвращает `MeasurementSubmissionResult`.
- **Services/DatabaseInventory** — вспомогательные записи: `DbObjectRequirement`, `InventorySnapshot`, `InventoryVerification`.
- **Services/DatabaseBootstrapper/BootstrapResult** — результат проверки (успех, профиль, первый запуск, код машины, путь к гайду).

### Модели
- **Models/MeasurementSeries** — список точек по активу/заводу, вычисляет тренд, умеет добавлять точки.
- **Models/AddMeasurementRequest** — запрос на добавление замера (завод, актив, точка).
- **Models/MeasurementSubmissionResult** — результат отправки (успех, сообщение, факт записи).

### ViewModel-слой
- **ViewModels/ViewModelBase** — базовый `ObservableObject`.
- **ViewModels/ThemePalette** — преднастроенные кисти для тёмной/светлой темы.
- **MainWindowViewModel** — хранит состояние подключения, создаёт дочерние панели после `OnConnectAsync(connString)` (KernelGateway, MeasurementDataProvider, MeasurementIngestionService и др.), статус подключения.
- **ConnectionFormViewModel** — поля подключения и `ConnectAsync` (RelayCommand), строит строку подключения из полей или берёт готовую.
- **CentralDataEntryViewModel** — команды `SaveAssetAsync`, `SavePolicyAsync`, `EnqueueEventAsync` через функции `fn_*` центральной БД; хранит статусы ввода.
- **AnalyticsPanelViewModel** — команды `RefreshAsync`, `IngestAsync`; `LoadAsync` тянет `fn_top_assets_by_cr`, вызывает `fn_asset_summary_json` для риска/завода, строит `AnalyticsRowViewModel`.
- **MeasurementsPanelViewModel** — команда `LoadAsync` (загрузка серий через `MeasurementDataProvider`), хранит `AddMeasurementFormViewModel`, делегирует отправку в `MeasurementIngestionService`, обновляет локальные серии.
- **AddMeasurementFormViewModel** — хранит список заводов/активов, валидирует ввод замера, команда `SubmitAsync` вызывает колбэк, добавляет новые активы/заводы при появлении.
- **DiagnosticsPanelViewModel** — подписка на LISTEN канал через центральный порт из `StoragePortFactory`; команды `StartAsync`/`StopAsync`, складывает `DiagnosticEntryViewModel`.
- **DiagnosticEntryViewModel** — строка диагностического события (время, заголовок, детали).
- **MeasurementAnalyticsEntryViewModel** — проекция точки замера для отображения (форматированные дата/толщина/примечание).
- **MeasurementModels** — `AddMeasurementFormViewModel` и `MeasurementAnalyticsEntryViewModel` используют `MeasurementPointDto`.
- **AddOperationFormViewModel** — UI-форма для вызовов команд ядра без прямых сервисов: варианты операций (`OperationOption` с `OperationKind`), команда `SubmitOperationAsync`, маршрутизация в `ExecuteAssetUpsertAsync`, `ExecutePolicyUpsertAsync`, `ExecuteEventEnqueueAsync`, простая валидация/JSON-проверка.
- **AnalyticsRowViewModel** — запись таблицы аналитики (asset, plant, CR, риск, обновление).

## OilErp.Tests.Runner
### Консольный раннер и окружение
- **Program** — точка входа, выполняет `DatabaseBootstrapper.EnsureProvisionedAsync`, запускает регистрируемые смоук-сценарии через `TestRunner`. Поддерживает CLI-команды (`add-asset`, `add-measurements-anpz`, `events-*`, `summary`, `top-by-cr`, `eval-risk`, `plant-cr`, `watch`). Внутри содержит `TestEnvironment` (кеширует `StorageConfig`, создаёт `StorageAdapter`/`KernelAdapter`, читает env и appsettings).

### Smoke-тесты
- **KernelSmoke** — `TestKernelOpensConnection`, `TestKernelExecutesHealthQuery`, `TestKernelTransactionLifecycle`.
- **StorageSmoke** — `TestCentralBulkDataSeedAndAnalytics`, `TestCentralEventQueueIntegrity`, `TestPlantCrStatsMatchesSeed`; использует `CentralHealthCheckScenario`, `HealthCheckDataSet`, `AssetExpectation`, `AnalyticsVerification`, `SimpleVerification`, `AssetSummarySnapshot`.
- **ExtendedSmokeTests** — `TestCalcCrFunctionMatchesLocalFormula`, `TestEvalRiskLevelsAlignWithPolicy`, `TestAssetSummaryJsonCompleteness`.
- **AsyncSmokeTests** — `TestFakeStorageConcurrentCommandsCounter`, `TestFakeStorageNotificationBroadcast`, `TestFakeStorageSubscribeUnsubscribe`, `TestListenNotifyRoundtrip`.
- **ValidationSmokeTests** — `TestDatabaseInventoryMatchesExpectations`, `TestConnectionProfileDetected`, `TestMissingObjectReminderFormatting`.
- **BootstrapSmokeTests** — `TestMachineCodeMarkerPersisted`.
- **ProfilesSmoke** — `TestAllProfilesInventory`, `TestPlantInsertAndFdwRoundtrip` (вставка через заводскую процедуру с откатом и проверкой FDW).

### TestDoubles
- **FakeStoragePort** — ин-мемори реализация `IStoragePort`: ведёт историю запросов/команд, счётчики вызовов, уведомления, транзакции (через `FakeTransaction`), искусственные задержки, события `Notified`, `Clear`.
- **FakeTransaction** — флаговый объект транзакции с `CommitAsync`/`RollbackAsync`/`DisposeAsync`.
- **TransactionalFakeStoragePort** — наследник FakeStoragePort с эмуляцией сейвпоинтов (стек `FakeSavepoint`), методами `CreateSavepoint`, `RollbackToSavepoint`, `ReleaseSavepoint`.
- **FakeSavepoint** — имя сейвпоинта, флаги отката/релиза.

### Утилиты
- **Util/TestRunner** — модели `TestResult`, `TestScenarioDefinition`, `TestRunScope`; регистрирует сценарии, выполняет их и печатает результаты.
- **Util/AppLogger** — минимальный логгер для тестов.
- **Util/FirstRunTracker** — хранит маркер первого запуска (аналог UI-версии).
- **Util/DatabaseBootstrapper** — та же логика, что в UI-версии: создаёт базы, проверяет схему, копирует гайд.
- **Util/DatabaseInventoryInspector** — аналог UI-версии: инвентаризация объектов БД, автоприменение SQL-скриптов по профилю, формат напоминаний.
