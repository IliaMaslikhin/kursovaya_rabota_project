# Описание классов и их взаимодействий

## OilErp.Core
- **Abstractions/DbClientBase** — базовый класс для клиентов БД, реализует `IStoragePort` и требует реализации `ExecuteQueryAsync<T>`, `ExecuteCommandAsync`, `BeginTransactionAsync`.
- **Abstractions/AppServiceBase** — базовый сервис, хранящий `IStoragePort` и проверяющий, что он не null.
- **Contracts/IStoragePort** — контракт доступа к БД: `ExecuteQueryAsync<T>(QuerySpec)`, `ExecuteCommandAsync(CommandSpec)`, `BeginTransactionAsync()`.
- **Contracts/IStorageTransaction** — асинхронная транзакция с `CommitAsync` и `RollbackAsync`.
- **Contracts/ICoreKernel** — ядро, отдаёт `IStoragePort` через свойство `Storage`.
- **Dto/CommandSpec, QuerySpec** — описание вызова функции/процедуры с именем операции, параметрами и опциональным тайм-аутом.
- **Dto/DatabaseProfile** — enum профилей (Central, PlantAnpz, PlantKrnpz, Unknown).
- **Dto/MeasurementPointDto** — точка замера (метка, время, толщина, комментарий).
- **Dto/OperationResult<T>** — результат операции с данными, ошибками и метаданными.
- **Operations/OperationNames** — константы имён SQL-функций/процедур для центральной БД и заводов.
- **Util/MeasurementBatchPayloadBuilder** — строит JSON payload для `sp_insert_measurement_batch`, нормализует точки/время и сортирует при необходимости.
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
- **FnAssetUpsertService** — `fn_asset_upsertAsync(asset_code, name, type, plant_code)` → затронутые строки.
- **FnPolicyUpsertService** — `fn_policy_upsertAsync(name, low, med, high)` → затронутые строки.
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
- **Adapters/StorageAdapter** — реализация `IStoragePort` на Npgsql. `ExecuteQueryAsync` и `ExecuteCommandAsync` получают метаданные функции/процедуры из каталога, формируют вызов с параметрами, различают JSON/SETOF/скаляры, логируют через `AppLogger`. `BeginTransactionAsync` создаёт соединение+транзакцию, хранит их в `AsyncLocal` для повторных вызовов. Внутренний `PgTransactionScope` реализует `IStorageTransaction` с `CommitAsync`, `RollbackAsync`, а также методами сейвпоинтов (Create/RollbackTo/Release) сверх интерфейса.
- **Adapters/KernelAdapter** — обёртка над `IStoragePort`, реализует `ICoreKernel.Storage`.
- **Config/StorageConfig** — record с `ConnectionString`, тайм-аутом и флагом `DisableRoutineMetadataCache` (чтение `OILERP__DB__DISABLE_PROC_CACHE`).
- **Logging/AppLogger** — минимальный логгер (консоль + файл в %APPDATA%/OilErp/logs); можно отключить консоль/файл через `OILERP__LOG__TO_CONSOLE`/`OILERP__LOG__TO_FILE` и задать каталог `OILERP__LOG__DIR` или программно вызвать `AppLogger.Configure`.

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
- **AnalyticsPanelViewModel** — команды `RefreshAsync`; `LoadAsync` тянет `fn_top_assets_by_cr`, вызывает `fn_asset_summary_json` для риска/завода, строит `AnalyticsRowViewModel`.
- **MeasurementsPanelViewModel** — команда `LoadAsync` (загрузка серий через `MeasurementDataProvider`), хранит `AddMeasurementFormViewModel`, делегирует отправку в `MeasurementIngestionService`, обновляет локальные серии.
- **AddMeasurementFormViewModel** — хранит список заводов/активов, валидирует ввод замера, команда `SubmitAsync` вызывает колбэк, добавляет новые активы/заводы при появлении.
- **MeasurementModels** — `AddMeasurementFormViewModel` использует `MeasurementPointDto`.
- **AnalyticsRowViewModel** — запись таблицы аналитики (asset, plant, CR, риск, обновление).

## OilErp.Tests.Runner
### Консольный раннер и окружение
- **Program** — точка входа, выполняет `DatabaseBootstrapper.EnsureProvisionedAsync`, запускает регистрируемые смоук-сценарии через `TestRunner`. Поддерживает CLI-команды (`add-asset`, `add-measurements-anpz`, `summary`, `top-by-cr`, `eval-risk`, `plant-cr`). Внутри содержит `TestEnvironment` (кеширует `StorageConfig`, создаёт `StorageAdapter`/`KernelAdapter`, читает env и appsettings).

### Smoke-тесты
- **KernelSmoke** — `TestKernelOpensConnection`, `TestKernelExecutesHealthQuery`, `TestKernelTransactionLifecycle`.
- **StorageSmoke** — `TestCentralBulkDataSeedAndAnalytics`, `TestPlantCrStatsMatchesSeed`; использует `CentralHealthCheckScenario`, `HealthCheckDataSet`, `AssetExpectation`, `AnalyticsVerification`, `AssetSummarySnapshot`.
- **ExtendedSmokeTests** — `TestCalcCrFunctionMatchesLocalFormula`, `TestEvalRiskLevelsAlignWithPolicy`, `TestAssetSummaryJsonCompleteness`.
- **AsyncSmokeTests** — `TestFakeStorageConcurrentCommandsCounter`.
- **ValidationSmokeTests** — `TestDatabaseInventoryMatchesExpectations`, `TestConnectionProfileDetected`, `TestMissingObjectReminderFormatting`.
- **BootstrapSmokeTests** — `TestMachineCodeMarkerPersisted`.
- **ProfilesSmoke** — `TestAllProfilesInventory`, `TestPlantInsertAndFdwRoundtrip` (вставка через заводскую процедуру с откатом и проверкой FDW).
- **PlantE2eSmokeTests** — убеждается, что батчи ANPZ/KRNPZ доходят до `central.measurement_batches` и обновляют `analytics_cr`; при отсутствии строк подключения помечает сценарий skipped.

### TestDoubles
- **FakeStoragePort** — ин-мемори реализация `IStoragePort`: ведёт историю запросов/команд, счётчики вызовов, транзакции (через `FakeTransaction`), искусственные задержки, `Clear`.
- **FakeTransaction** — флаговый объект транзакции с `CommitAsync`/`RollbackAsync`/`DisposeAsync`.
- **TransactionalFakeStoragePort** — наследник FakeStoragePort с эмуляцией сейвпоинтов (стек `FakeSavepoint`), методами `CreateSavepoint`, `RollbackToSavepoint`, `ReleaseSavepoint`.
- **FakeSavepoint** — имя сейвпоинта, флаги отката/релиза.

### Утилиты
- **Util/TestRunner** — модели `TestResult`, `TestScenarioDefinition`, `TestRunScope`; регистрирует сценарии, выполняет их и печатает результаты.
- **Util/AppLogger** — минимальный логгер для тестов.
- **Util/FirstRunTracker** — хранит маркер первого запуска (аналог UI-версии).
- **Util/DatabaseBootstrapper** — та же логика, что в UI-версии: создаёт базы, проверяет схему, копирует гайд.
- **Util/DatabaseInventoryInspector** — аналог UI-версии: инвентаризация объектов БД, автоприменение SQL-скриптов по профилю, формат напоминаний.
