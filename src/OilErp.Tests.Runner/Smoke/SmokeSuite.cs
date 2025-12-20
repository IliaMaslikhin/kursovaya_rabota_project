using OilErp.Bootstrap;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Services.Aggregations;
using OilErp.Core.Services.Central;
using OilErp.Core.Services.Plants.ANPZ;
using OilErp.Infrastructure.Adapters;
using OilErp.Infrastructure.Config;
using OilErp.Tests.Runner.Smoke;
using OilErp.Tests.Runner.Util;

namespace OilErp.Tests.Runner;

/// <summary>
/// Собирает и запускает смоук-тесты, отдаёт краткий итог.
/// </summary>
public static class SmokeSuite
{
    /// <summary>
    /// Прогоняет все сценарии и возвращает сводку.
    /// </summary>
    public static async Task<SmokeSuiteResult> RunAsync()
    {
        var bootstrapper = new DatabaseBootstrapper(TestEnvironment.ConnectionString);
        var bootstrap = await bootstrapper.EnsureProvisionedAsync();
        if (!bootstrap.Success)
        {
            var reason = $"Bootstrap не прошёл: {bootstrap.ErrorMessage}";
            AppLogger.Error($"[smoke] {reason}");
            return SmokeSuiteResult.Fail(reason, bootstrap);
        }

        // Подчищаем мусор от старой версии смоук‑тестов (когда имена были фиксированными).
        // Это нужно, чтобы база не "засорялась" тестовыми активами после неудачных запусков.
        try
        {
            await HealthCheckSeedContext.CleanupArtifactsAsync(
                TestEnvironment.ConnectionString,
                policyName: "HC_SMOKE_POLICY",
                assetCodes: new[] { "HC_UNIT_OK", "HC_UNIT_LOW", "HC_UNIT_MED", "HC_UNIT_HIGH" },
                ct: CancellationToken.None);
        }
        catch
        {
            // Если чистка не удалась — не валим всё приложение, просто продолжаем.
        }

        var runner = BuildRunner(bootstrap);
        var results = await runner.RunAllAsync();
        var executed = results.Where(r => !r.Result.Skipped).ToList();
        var failed = executed.Where(r => !r.Result.Success).ToList();
        var summary = $"Смоук-тесты: пройдено {executed.Count - failed.Count}/{executed.Count}, пропущено {results.Count - executed.Count}";

        if (failed.Count > 0)
        {
            var details = string.Join("; ", failed.Select(f => $"{f.Definition.Title}: {f.Result.Error ?? f.Definition.FailureHint}"));
            AppLogger.Error($"[smoke] есть провалы: {details}");
            return SmokeSuiteResult.Fail(summary + $" · {details}", bootstrap, results);
        }

        AppLogger.Info($"[smoke] все проверки прошли ({summary})");
        return SmokeSuiteResult.Ok(summary, bootstrap, results);
    }

    private static TestRunner BuildRunner(BootstrapResult bootstrap)
    {
        var runner = new TestRunner
        {
            IsFirstRun = bootstrap.IsFirstRun,
            MachineCode = bootstrap.MachineCode
        };

        var kernelSmoke = new KernelSmoke();
        var storageSmoke = new StorageSmoke();
        var extendedSmoke = new ExtendedSmokeTests();
        var commonSmoke = new CommonSmokeTests();
        var validationSmoke = new ValidationSmokeTests();
        var profilesSmoke = new ProfilesSmoke();
        var plantE2eSmoke = new PlantE2eSmokeTests();

        const string CategoryConnection = "Подключение к базе";
        const string CategoryIngestion = "Загрузка данных";
        const string CategoryAnalytics = "Аналитика и отчёты";
        const string CategoryReliability = "Надёжность и асинхронность";
        const string CategoryValidation = "Валидация окружения";

        void RegisterScenario(string name, string category, string title, string successHint, string failureHint, TestScenario scenario, TestRunScope scope = TestRunScope.Always) =>
            runner.Register(new TestScenarioDefinition(name, category, title, successHint, failureHint, scenario, scope));

        // Подключение
        RegisterScenario("Kernel_Opens_Connection", CategoryConnection, "Проверка подключения ядра", "Соединение с PostgreSQL установлено", "Нет соединения с базой", kernelSmoke.TestKernelOpensConnection);
        RegisterScenario("Kernel_Executes_Health_Query", CategoryConnection, "Health-запрос через ядро", "Health-запрос отработал", "Health-запрос через ядро не выполняется", kernelSmoke.TestKernelExecutesHealthQuery);
        RegisterScenario("Kernel_Transaction_Lifecycle", CategoryConnection, "Цикл транзакций ядра", "Begin/commit/rollback работают", "Транзакции ядра не выполняются", kernelSmoke.TestKernelTransactionLifecycle);

        // Загрузка и очередь
        RegisterScenario("Central_Bulk_Data_Seed_And_Analytics", CategoryIngestion, "Массовая загрузка и аналитика", "Посев данных и аналитика завершены", "Конвейер загрузки/аналитики не завершён", storageSmoke.TestCentralBulkDataSeedAndAnalytics);
        RegisterScenario("Plant_Cr_Stats_Match_Seed", CategoryAnalytics, "CR-статистика по заводу", "Среднее/P90 совпадают с посевом", "CR-статистика отличается от ожидаемой", storageSmoke.TestPlantCrStatsMatchesSeed);

        // Аналитика и отчёты
        RegisterScenario("CalcCr_Function_Matches_Local", CategoryAnalytics, "Расчёт скорости коррозии", "CR соответствует эталонной формуле", "CR расходится с эталоном", extendedSmoke.TestCalcCrFunctionMatchesLocalFormula);
        RegisterScenario("EvalRisk_Levels_Align_With_Policy", CategoryAnalytics, "Соответствие уровней риска", "Риски соответствуют политике", "Риски не совпадают с политикой", extendedSmoke.TestEvalRiskLevelsAlignWithPolicy);
        RegisterScenario("Asset_Summary_Json_Completeness", CategoryAnalytics, "JSON-сводка актива", "Сводка содержит необходимые блоки", "JSON-сводка неполная", extendedSmoke.TestAssetSummaryJsonCompleteness);

        // Надёжность и асинхронность
        RegisterScenario("Fake_Concurrent_Commands_Counter", CategoryReliability, "Счётчик параллельных команд", "Фейковый порт считает параллельные вызовы", "Счётчик параллельных команд некорректен", commonSmoke.TestFakeStorageConcurrentCommandsCounter);

        // Валидация окружения
        RegisterScenario("Database_Inventory_Matches_Expectations", CategoryValidation, "Полнота объектной схемы", "Все обязательные объекты присутствуют", "Отсутствуют обязательные объекты", validationSmoke.TestDatabaseInventoryMatchesExpectations, TestRunScope.FirstRunOnly);
        RegisterScenario("Machine_Code_Marker", CategoryValidation, "Маркер первого запуска", "Код машины сохранён", "Маркер первого запуска не записан", commonSmoke.TestMachineCodeMarkerPersisted, TestRunScope.FirstRunOnly);
        RegisterScenario("Connection_Profile_Detected", CategoryValidation, "Определение профиля подключения", "Профиль базы распознан", "Не удалось определить профиль базы", validationSmoke.TestConnectionProfileDetected);
        RegisterScenario("Missing_Object_Reminder_Formatting", CategoryValidation, "Шаблон напоминания о недостающих объектах", "Формат напоминания соответствует требованиям", "Шаблон напоминания некорректен", validationSmoke.TestMissingObjectReminderFormatting);
        RegisterScenario("Profiles_Inventory_All", CategoryValidation, "Проверка схем по профилям", "Профили central/anpz/krnpz содержат обязательные объекты", "В профилях отсутствуют объекты", profilesSmoke.TestAllProfilesInventory);
        RegisterScenario("Plant_Insert_And_FDW_Roundtrip", CategoryValidation, "Проверка завода и FDW", "Процедура вставки и FDW работают с откатом", "Ошибка вставки/FDW на заводе", profilesSmoke.TestPlantInsertAndFdwRoundtrip);

        // E2E
        RegisterScenario("Plant_Events_Reach_Analytics", CategoryIngestion, "Заводские события доходят до analytics_cr", "Ingest обновляет аналитику и очищает очередь", "События не доходят до analytics_cr или остаются в очереди", plantE2eSmoke.TestPlantEventsReachAnalytics);
        RegisterScenario("Plant_Insert_Validation_Fails", CategoryValidation, "Валидация заводской функции", "Ошибка при пустом asset_code", "Вставка прошла без ошибки", commonSmoke.TestPlantInsertValidationFails);

        return runner;
    }
}

public sealed record SmokeSuiteResult(
    bool Success,
    string Summary,
    BootstrapResult? Bootstrap,
    IReadOnlyList<(TestScenarioDefinition Definition, TestResult Result)>? Results)
{
    public static SmokeSuiteResult Ok(string summary, BootstrapResult bootstrap, IReadOnlyList<(TestScenarioDefinition, TestResult)> results) =>
        new(true, summary, bootstrap, results);

    public static SmokeSuiteResult Fail(string summary, BootstrapResult bootstrap, IReadOnlyList<(TestScenarioDefinition, TestResult)>? results = null) =>
        new(false, summary, bootstrap, results);
}
