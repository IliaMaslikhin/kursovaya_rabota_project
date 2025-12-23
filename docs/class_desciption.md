# Описание классов и их взаимодействий

Документ описывает ключевые классы/модули проекта, их связи и важные методы. Имена методов приведены так, как они называются в коде.

## OilErp.Core (контракты, DTO, сервисы)

### Contracts
- `IStoragePort` — главный контракт доступа к БД. Важные методы: `ExecuteQueryAsync<T>(QuerySpec)`, `ExecuteCommandAsync(CommandSpec)`, `BeginTransactionAsync()`.
- `IStorageTransaction` — контракт транзакции. Важные методы: `CommitAsync()`, `RollbackAsync()`, `DisposeAsync()`.
- `ICoreKernel` — минимальный интерфейс ядра с единственным свойством `Storage` (ссылка на `IStoragePort`).

### Abstractions
- `DbClientBase` — базовый абстрактный класс для клиентов БД. Определяет виртуальный контракт реализации `IStoragePort`.
- `AppServiceBase` — общий базовый класс сервисов. Держит `IStoragePort` и методы нормализации ввода: `NormalizeOptional`, `NormalizeCode`, `NormalizePlant`.

### DTO и спецификации
- `CommandSpec`, `QuerySpec` — описатели операций (имя операции + параметры + тайм‑аут). Используются всеми сервисами Core.
- `OperationResult<T>` — обертка результата (успех/ошибка/строки) для сценариев, где нужен унифицированный ответ.
- `DatabaseProfile` — перечисление профилей: `Central`, `PlantAnpz`, `PlantKrnpz`, `Unknown`.
- `MeasurementPointDto` — точка замера (Label, Ts, Thickness, Note).
- Аналитические DTO: `EvalRiskRowDto`, `PlantCrStatDto`, `PlantCrAggregateDto`, `AssetSummaryDto`, `TopAssetCrDto`.

### Operations
- `OperationNames` — константы SQL‑операций в формате `schema.name`.
  - `OperationNames.Plant.*` — заводские операции (`sp_insert_measurement_batch`, `sp_insert_measurement_batch_prc`).
  - `OperationNames.Central.*` — central‑операции (`fn_*` и `sp_*`).

### Сервисы central (обертки над SQL)
Все сервисы наследуют `AppServiceBase` и используют `QuerySpec`/`CommandSpec`:
- `FnCalcCrService.fn_calc_crAsync` — вызывает `public.fn_calc_cr`.
- `FnAssetUpsertService.fn_asset_upsertAsync` — вызывает `public.fn_asset_upsert` (апсерт актива).
- `FnPolicyUpsertService.fn_policy_upsertAsync` — вызывает `public.fn_policy_upsert` (апсерт политики).
- `FnEvalRiskService.fn_eval_riskAsync` — вызывает `public.fn_eval_risk`, мапит словари в `EvalRiskRowDto`.
- `FnAssetSummaryJsonService.fn_asset_summary_jsonAsync` — вызывает `public.fn_asset_summary_json`, десериализует JSON в `AssetSummaryDto`.
- `FnTopAssetsByCrService.fn_top_assets_by_crAsync` — вызывает `public.fn_top_assets_by_cr`, мапит строки в `TopAssetCrDto`.
- `FnPlantCrStatsService.fn_plant_cr_statsAsync` — вызывает `public.fn_plant_cr_stats`, мапит строки в `PlantCrStatDto`.
- `SpPolicyUpsertService.sp_policy_upsertAsync` — вызов процедуры `public.sp_policy_upsert`.
- `SpAssetUpsertService.sp_asset_upsertAsync` — вызов процедуры `public.sp_asset_upsert`.

### Сервисы заводов
- `Plants.ANPZ.SpInsertMeasurementBatchService.sp_insert_measurement_batchAsync` — обертка над `public.sp_insert_measurement_batch` (ANPZ).
- `Plants.ANPZ.SpInsertMeasurementBatchPrcService.sp_insert_measurement_batch_prcAsync` — обертка над процедурой `public.sp_insert_measurement_batch_prc` (ANPZ).
- `Plants.KRNPZ.*` — те же методы для KRNPZ (с нормализацией кода завода).

### Aggregations
- `PlantCrService.GetPlantCrAsync` — агрегирует CR по заводу через `FnPlantCrStatsService` и возвращает `PlantCrAggregateDto`.

### Util
- `MeasurementBatchPayloadBuilder` — формирует JSON‑массив точек для заводских процедур.
  - Важные методы: `BuildJson(IEnumerable<MeasurementPointDto>)`, `BuildJson(MeasurementPointDto)`.
  - Внутри: нормализация меток и временных меток (`NormalizePoint`, `NormalizeTimestamp`).

## OilErp.Infrastructure (Npgsql, bootstrap, конфиги)

### Config
- `StorageConfig` — record с `ConnectionString`, `CommandTimeoutSeconds`, `DisableRoutineMetadataCache`.
- `StorageConfigProvider.GetConfig` — читает строки окружения (`OILERP__DB__CONN[_ANPZ|_KRNPZ]`, `OIL_ERP_PG*`) и тайм‑аут.

### Adapters
- `StorageAdapter` — реализация `IStoragePort` на Npgsql.
  - `ExecuteQueryAsync<T>`: читает метаданные функции/процедуры (`pg_proc`), выбирает режим (FUNCTION/PROCEDURE/SETOF/JSON), читает результаты в `Dictionary<string, object?>` и проецирует в `T`.
  - `ExecuteCommandAsync`: вызывает процедуру через `CALL` или функцию через `SELECT`, возвращает число затронутых строк.
  - `BeginTransactionAsync`: открывает отдельное соединение и хранит его в `AsyncLocal`.
  - Важные внутренние методы: `GetRoutineMetadataAsync`, `BuildRoutineCommand`, `BuildArgumentList`, `LooksLikeJsonParam`.
  - Кэширует метаданные процедур/функций (`RoutineCache`) и умеет инвалидировать кэш при ошибке SQL.
  - Вложенный `PgTransactionScope` реализует `IStorageTransaction` и гарантирует rollback в `DisposeAsync`.
- `KernelAdapter` — простая оболочка для `ICoreKernel`, хранит `IStoragePort`.

### Util / Bootstrap
- `AppLogger` — минимальный логгер (консоль + файл). Важные методы: `Configure`, `Info`, `Error`.
- `FirstRunTracker` — хранит маркер первого запуска в `%APPDATA%/OilErp/first-run.machine`.
- `DatabaseBootstrapper` — общий bootstrap для UI/Tests:
  - `EnsureProvisionedAsync` создает базы `central/anpz/krnpz`, вызывает `DatabaseInventoryInspector`, пишет гайд на рабочий стол при ошибке.
  - `EnsureDatabasesAsync` создает отсутствующие БД через подключение к `postgres`.
- `DatabaseInventoryInspector` — инвентаризация и автосинхронизация схемы:
  - `VerifyAsync` сверяет функции/процедуры/таблицы/триггеры.
  - `TryAutoApplyProfileScriptsAsync` автоматически применяет профильные SQL из `sql/`.
  - `EnsurePlantFdwMappingAsync` правит FDW‑сервер `central_srv` и user mapping на заводе.
- `DatabaseInventory` содержит записи `DbObjectRequirement`, `InventorySnapshot`, `InventoryVerification`.

## OilErp.Ui (Avalonia UI)

### Точка входа и инфраструктура UI
- `Program.Main` — запускает смоук‑suite (`SmokeSuite.RunAsync`) и только потом стартует Avalonia.
- `App` — инициализирует Avalonia, включает тему по умолчанию через `ThemeManager`.
- `ViewLocator` — отражением мапит `ViewModel` → `View` по имени.
- `Converters`:
  - `BoolInvertConverter` — инверсия bool для биндингов.
  - `DatabaseProfileDisplayConverter` — отображение профиля БД (Central/ANPZ/KNPZ).

### Services
- `KernelGateway` — точка входа в Core/Infra для UI:
  - `Create` запускает bootstrap, валидирует `fn_calc_cr`, создает `StorageAdapter`.
  - Возвращает `Storage`, `StorageFactory`, `BootstrapInfo`, `StatusMessage`.
- `StoragePortFactory` — выдаёт порты для central или заводов на основе окружения.
- `MeasurementDataProvider` — читает данные central через `FnTopAssetsByCrService` + `FnAssetSummaryJsonService`.
  Возвращает `MeasurementDataResult` (серии + статусное сообщение).
- `MeasurementIngestionService` — формирует JSON через `MeasurementBatchPayloadBuilder` и вызывает заводские `sp_insert_measurement_batch`.
- `MeasurementSnapshotService` — читает локальные файлы `Data/*_measurements.json` в `MeasurementSeries` (в текущем UI не активирован в загрузке, но сервис готов).
- `UiDialogHost` / `UiFilePicker` — диалоги и file picker.
- `UiSettingsStore` — хранение UI‑настроек (политики риска по заводам).
- `UiSettings` — record‑контейнер политик риска и последней выбранной политики.
- `SimpleXlsxWriter` — ручная генерация XLSX (zip‑архив с XML).
- `ThemeManager` — применяет `ThemePalette` в ресурсы Avalonia.

### Models
- `AddMeasurementRequest` — входная модель отправки замера (plant, asset, measurement).
- `MeasurementSeries` — контейнер для набора `MeasurementPointDto` по активу.
- `MeasurementSubmissionResult` — результат сохранения замера (успех/сообщение/признак записи).

### ViewModels: оболочка окна и подключение
- `MainWindowViewModel`
  - Принимает `KernelGateway` и строит нужные вкладки по профилю.
  - Важные методы: `ChangeConnectionCommand`, обработчик темы `OnSelectedThemeChanged`.
- `ConnectWindowViewModel`
  - Создаёт `ConnectionFormViewModel` и по `ConnectAsync` открывает `MainWindow`.
- `ConnectionFormViewModel`
  - Формирует строку подключения (`BuildConnectionString`).
  - `ConnectAsync` и `TestAsync` тестируют подключение.

### ViewModels: central (оборудование, политики, замеры)
- `CentralEquipmentTabViewModel`
  - `RefreshAsync` грузит список `assets_global`.
  - `AddAsync`/`EditAsync`/`DeleteAsync` — управление справочником central.
  - `UpsertAsync` вызывает `FnAssetUpsertService`.
- `CentralPoliciesTabViewModel`
  - `RefreshAsync` читает `risk_policies`.
  - `AddAsync`/`EditAsync`/`DeleteAsync` — управление политиками.
  - `UpsertAsync` вызывает `FnPolicyUpsertService`.
- `CentralMeasurementsTabViewModel`
  - `RefreshAsync` строит матрицу последних замеров по `measurement_batches`.
  - `AddMeasurementAsync` добавляет замер в central (вставка в `measurement_batches`).
  - `OpenTransferAsync` открывает окно импорта/экспорта.
  - Внутренние методы: `GenerateNextLabelAsync`, `InsertBatchAsync`, `LoadLastAnalyticsAsync`, `HasExtendedColumnsAsync`.

#### Вспомогательные модели central
- `EquipmentItemViewModel` — строка списка оборудования (код/название/тип/источник).
- `PolicyItemViewModel` — строка списка политик (name/low/med/high).
- `CentralMeasurementColumnViewModel` — колонка даты в таблице замеров.
- `CentralMeasurementEquipmentRowViewModel` — строка оборудования + словарь значений по датам, метод `RebuildCells`.
- `CentralMeasurementsGroupHeaderViewModel` — заголовок группировки (завод/тип/дата).

### ViewModels: заводы
- `PlantEquipmentTabViewModel`
  - `RefreshAsync` читает `assets_local`.
  - `AddAsync`/`EditAsync`/`DeleteAsync` — управление локальными активами.
- `PlantMeasurementsTabViewModel`
  - `RefreshAsync` строит матрицу локальных замеров.
  - `AddMeasurementAsync` вызывает заводские `sp_insert_measurement_batch`.
  - `EditLastMeasurementAsync` и `DeleteLastMeasurementAsync` правят последний замер и отправляют агрегированный батч в central.
  - `OpenTransferAsync` открывает окно импорта/экспорта для завода.
  - Внутренние методы: `EnqueueBatchToCentralAsync`, `GenerateNextLabelAsync`, `ShouldBumpTimestamp`.

#### Вспомогательные модели заводов
- `PlantEquipmentItemViewModel` — строка списка локального оборудования (код/локация/статус/created_at).
- `PlantMeasurementColumnViewModel` — колонка даты в локальной таблице.
- `PlantMeasurementEquipmentRowViewModel` — строка оборудования с локальными замерами и пересчетом `Cells`.
- `PlantMeasurementsGroupHeaderViewModel` — заголовок группировки по статусу/локации/дате.
- `EquipmentSortOption` / `EquipmentGroupOption` — модели опций сортировки/группировки (используются и в central, и на заводах).

### ViewModels: история и перенос данных
- `CentralMeasurementsTransferWindowViewModel`
  - Экспорт/импорт CSV/JSON/XLSX по `measurement_batches`.
  - Вставка идет напрямую в `measurement_batches` с пересчетом prev/last.
- `PlantMeasurementsTransferWindowViewModel`
  - Экспорт/импорт CSV/JSON/XLSX по локальным `measurements`.
  - Импорт вызывает `sp_insert_measurement_batch`.
- `CentralMeasurementHistoryWindowViewModel`
  - История `measurement_batches` по активу: фильтры по времени, группировка, импорт/экспорт.
- `PlantMeasurementHistoryWindowViewModel`
  - История `measurements` по активу: редактирование/удаление и отправка батча в central.

#### Вспомогательные модели истории
- `CentralMeasurementHistoryItemViewModel` — строка истории central (plant/ts/thickness/label/note).
- `CentralMeasurementHistoryGroupHeaderViewModel` — заголовок группировки по дню.
- `PlantMeasurementHistoryItemViewModel` — строка истории завода.
- `PlantMeasurementHistoryGroupHeaderViewModel` — заголовок по дню.
- `MeasurementSortOption` — опции сортировки (используются в обоих окнах истории).

### ViewModels: аналитика
- `AnalyticsPanelViewModel`
  - Загружает политики риска и строит группы по заводам.
  - `RefreshAsync`, `ApplyPolicyToAllAsync`, `RefreshGroupAsync`.
- `AnalyticsPlantGroupViewModel`
  - Представляет одну группу аналитики (завод/политика/таблица).
- `AnalyticsRowViewModel` — строка таблицы аналитики (asset/plant/cr/risk/updated_at).

### Дополнительные ViewModels
- `MeasurementsPanelViewModel` — загрузка серий через `MeasurementDataProvider`, отправка замеров через `MeasurementIngestionService`.
- `AddMeasurementFormViewModel` — форма ввода (команда `SubmitAsync`) и управление списком активов/заводов.
- `EquipmentEditWindowViewModel`, `RiskPolicyEditWindowViewModel`, `CentralMeasurementEditWindowViewModel`, `PlantMeasurementEditWindowViewModel` — диалоговые формы с валидацией ввода.
- `ConfirmDialogViewModel` — универсальное подтверждение действия.
- `ThemePalette`, `ThemeOption` — модели тем оформления UI.

## OilErp.Tests.Runner (смоук‑проверки)

### Основные классы
- `SmokeSuite.RunAsync` — запускает `DatabaseBootstrapper`, регистрирует сценарии в `TestRunner` и возвращает сводный результат.
- `TestEnvironment` — создание `StorageAdapter`/`KernelAdapter` и чтение конфигов (env + appsettings).
- `TestRunner` — реестр сценариев, группировка по категориям, вычисление итогов.

### Smoke‑тесты
- `KernelSmoke` — проверка подключения, базового запроса и транзакций ядра.
- `StorageSmoke` — массовый посев и проверка аналитики, сверка mean/p90.
- `ExtendedSmokeTests` — проверка `fn_calc_cr`, `fn_eval_risk`, полноты `fn_asset_summary_json`.
- `CommonSmokeTests` — проверка фейкового порта, валидация ошибок и маркера первого запуска.
- `ValidationSmokeTests` — инвентаризация схемы и проверка шаблона напоминаний.
- `ProfilesSmoke` — проверка профилей и доступности FDW.
- `PlantE2eSmokeTests` — E2E: завод → central → analytics_cr.

### Внутренние модели smoke‑сценариев
- `HealthCheckDataSet` — набор тестовых активов и порогов риска для посева.
- `AssetMeasurementSeed` — входные измерения для актива (prev/last).
- `AssetExpectation` — ожидаемые CR и риск по активу.
- `AssetAnalyticsRow` — строка сравнения ожиданий и факта (CR/risk/updated_at).
- `HealthCheckSeedSnapshot` — снимок ожиданий и списка тестовых активов.
- `AnalyticsVerification` / `SimpleVerification` — результат проверки аналитики.
- `CentralHealthCheckScenario` — сценарий посева/проверки, использует `StorageAdapter` и прямые SQL.
- `HealthCheckSeedContext` — контекст посева с авто‑очисткой (rollback + cleanup).
- `AssetSummarySnapshot` — парсер JSON‑сводки (уровень риска, updated_at).

### TestDoubles и утилиты
- `FakeStoragePort`/`FakeTransaction` — фейковые реализации `IStoragePort`/`IStorageTransaction`.
- `TransactionalFakeStoragePort` — обертка над реальным портом с логированием вызовов.
- `MeasurementBatchHelper` — читает CSV/JSON и строит JSON для заводских процедур.

## Типовые цепочки взаимодействий классов
- Подключение UI: `ConnectWindowViewModel` → `KernelGateway.Create` → `DatabaseBootstrapper` → `DatabaseInventoryInspector` → `StorageAdapter`.
- Добавление замера на заводе: `PlantMeasurementsTabViewModel` → `MeasurementBatchPayloadBuilder` → `SpInsertMeasurementBatchService` → `StorageAdapter` → `sp_insert_measurement_batch` → `sp_insert_measurement_batch_prc` → `central_ft.measurement_batches`.
- Добавление замера в central: `CentralMeasurementsTabViewModel` → `InsertBatchAsync` → `measurement_batches` → `trg_measurement_batches_bi` → `analytics_cr`.
- Импорт CSV/JSON в central: `CentralMeasurementsTransferWindowViewModel` → `ParseCsv/ParseJson` → `InsertPointsAsync` → `InsertBatchAsync`.
- Редактирование истории на заводе: `PlantMeasurementHistoryWindowViewModel` → `UpdateMeasurementAsync/DeleteMeasurementAsync` → `EnqueueBatchToCentralAsync`.
- Аналитика: `AnalyticsPanelViewModel` → `RefreshGroupAsync` → Npgsql‑запрос → `AnalyticsRowViewModel`.
- Запуск smoke‑suite: `Program` → `SmokeSuite.RunAsync` → `TestRunner` → классы `KernelSmoke`/`StorageSmoke`/`PlantE2eSmokeTests`.
