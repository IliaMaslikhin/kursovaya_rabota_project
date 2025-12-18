using OilErp.Bootstrap;
using OilErp.Tests.Runner.Smoke;
using OilErp.Tests.Runner.Util;
using OilErp.Infrastructure.Adapters;
using OilErp.Core.Services.Central;
using OilErp.Core.Services.Plants.ANPZ;
using System.Text;
using System.Text.Json;
using System.Globalization;
using OilErp.Core.Services.Dtos;
using OilErp.Core.Services.Aggregations;
using OilErp.Infrastructure.Config;
using System.Linq;
using System.Reflection;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;

namespace OilErp.Tests.Runner;

/// <summary>
/// Console application entry point for smoke tests
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            var bootstrapper = new DatabaseBootstrapper(TestEnvironment.ConnectionString);
            var bootstrap = await bootstrapper.EnsureProvisionedAsync();
            if (!bootstrap.Success)
            {
                Console.WriteLine($"[Bootstrap] Ошибка проверки/создания БД: {bootstrap.ErrorMessage}");
                if (!string.IsNullOrWhiteSpace(bootstrap.GuidePath))
                {
                    Console.WriteLine($"[Bootstrap] Инструкция сохранена: {bootstrap.GuidePath}");
                }

                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine($"[Bootstrap] Профиль={bootstrap.Profile} код машины={bootstrap.MachineCode} {(bootstrap.IsFirstRun ? "(первый запуск)" : string.Empty)}");

            if (args.Length > 0)
            {
                var exit = await HandleCliAsync(args);
                Environment.ExitCode = exit ? 0 : 1;
                return;
        }

        Console.WriteLine("Расширенные смоук-тесты OilErp");
        Console.WriteLine("============================");
        Console.WriteLine();

        var runner = new TestRunner();
        runner.IsFirstRun = bootstrap.IsFirstRun;
        runner.MachineCode = bootstrap.MachineCode;
        var kernelSmoke = new KernelSmoke();
        var storageSmoke = new StorageSmoke();
        var extendedSmoke = new ExtendedSmokeTests();
        var asyncSmoke = new AsyncSmokeTests();
        var bootstrapSmoke = new BootstrapSmokeTests();
        var validationSmoke = new ValidationSmokeTests();
        var profilesSmoke = new ProfilesSmoke();
        var negativeSmoke = new NegativeSmokeTests();
        var plantE2eSmoke = new PlantE2eSmokeTests();

        const string CategoryConnection = "Подключение к базе";
        const string CategoryIngestion = "Загрузка данных";
        const string CategoryAnalytics = "Аналитика и отчёты";
        const string CategoryReliability = "Надёжность и асинхронность";
        const string CategoryValidation = "Валидация окружения";

        void RegisterScenario(string name, string category, string title, string successHint, string failureHint, TestScenario scenario, TestRunScope scope = TestRunScope.Always) =>
            runner.Register(new TestScenarioDefinition(name, category, title, successHint, failureHint, scenario, scope));

        // Подключение
        RegisterScenario(
            "Kernel_Opens_Connection",
            CategoryConnection,
            "Проверка подключения ядра",
            "Соединение с PostgreSQL установлено",
            "Нет соединения с базой",
            kernelSmoke.TestKernelOpensConnection);
        RegisterScenario(
            "Kernel_Executes_Health_Query",
            CategoryConnection,
            "Health-запрос через ядро",
            "Health-запрос отработал",
            "Health-запрос через ядро не выполняется",
            kernelSmoke.TestKernelExecutesHealthQuery);
        RegisterScenario(
            "Kernel_Transaction_Lifecycle",
            CategoryConnection,
            "Цикл транзакций ядра",
            "Begin/commit/rollback работают",
            "Транзакции ядра не выполняются",
            kernelSmoke.TestKernelTransactionLifecycle);

        // Загрузка и очередь
        RegisterScenario(
            "Central_Bulk_Data_Seed_And_Analytics",
            CategoryIngestion,
            "Массовая загрузка и аналитика",
            "Посев данных и аналитика завершены",
            "Конвейер загрузки/аналитики не завершён",
            storageSmoke.TestCentralBulkDataSeedAndAnalytics);
        RegisterScenario(
            "Plant_Cr_Stats_Match_Seed",
            CategoryAnalytics,
            "CR-статистика по заводу",
            "Среднее/P90 совпадают с посевом",
            "CR-статистика отличается от ожидаемой",
            storageSmoke.TestPlantCrStatsMatchesSeed);

        // Аналитика и отчёты
        RegisterScenario(
            "CalcCr_Function_Matches_Local",
            CategoryAnalytics,
            "Расчёт скорости коррозии",
            "CR соответствует эталонной формуле",
            "CR расходится с эталоном",
            extendedSmoke.TestCalcCrFunctionMatchesLocalFormula);
        RegisterScenario(
            "EvalRisk_Levels_Align_With_Policy",
            CategoryAnalytics,
            "Соответствие уровней риска",
            "Риски соответствуют политике",
            "Риски не совпадают с политикой",
            extendedSmoke.TestEvalRiskLevelsAlignWithPolicy);
        RegisterScenario(
            "Asset_Summary_Json_Completeness",
            CategoryAnalytics,
            "JSON-сводка актива",
            "Сводка содержит необходимые блоки",
            "JSON-сводка неполная",
            extendedSmoke.TestAssetSummaryJsonCompleteness);

        // Надёжность и асинхронность
        RegisterScenario(
            "Fake_Concurrent_Commands_Counter",
            CategoryReliability,
            "Счётчик параллельных команд",
            "Фейковый порт считает параллельные вызовы",
            "Счётчик параллельных команд некорректен",
            asyncSmoke.TestFakeStorageConcurrentCommandsCounter);

        // Валидация окружения
        RegisterScenario(
            "Database_Inventory_Matches_Expectations",
            CategoryValidation,
            "Полнота объектной схемы",
            "Все обязательные объекты присутствуют",
            "Отсутствуют обязательные объекты",
            validationSmoke.TestDatabaseInventoryMatchesExpectations,
            TestRunScope.FirstRunOnly);
        RegisterScenario(
            "Machine_Code_Marker",
            CategoryValidation,
            "Маркер первого запуска",
            "Код машины сохранён",
            "Маркер первого запуска не записан",
            bootstrapSmoke.TestMachineCodeMarkerPersisted,
            TestRunScope.FirstRunOnly);
        RegisterScenario(
            "Connection_Profile_Detected",
            CategoryValidation,
            "Определение профиля подключения",
            "Профиль базы распознан",
            "Не удалось определить профиль базы",
            validationSmoke.TestConnectionProfileDetected);
        RegisterScenario(
            "Missing_Object_Reminder_Formatting",
            CategoryValidation,
            "Шаблон напоминания о недостающих объектах",
            "Формат напоминания соответствует требованиям",
            "Шаблон напоминания некорректен",
            validationSmoke.TestMissingObjectReminderFormatting);
        RegisterScenario(
            "Profiles_Inventory_All",
            CategoryValidation,
            "Проверка схем по профилям",
            "Профили central/anpz/krnpz содержат обязательные объекты",
            "В профилях отсутствуют объекты",
            profilesSmoke.TestAllProfilesInventory);
        RegisterScenario(
            "Plant_Insert_And_FDW_Roundtrip",
            CategoryValidation,
            "Проверка завода и FDW",
            "Процедура вставки и FDW работают с откатом",
            "Ошибка вставки/FDW на заводе",
            profilesSmoke.TestPlantInsertAndFdwRoundtrip);

        RegisterScenario(
            "Plant_Events_Reach_Analytics",
            CategoryIngestion,
            "Заводские события доходят до analytics_cr",
            "Ingest обновляет аналитику и очищает очередь",
            "События не доходят до analytics_cr или остаются в очереди",
            plantE2eSmoke.TestPlantEventsReachAnalytics);

        RegisterScenario(
            "Plant_Insert_Validation_Fails",
            CategoryValidation,
            "Валидация заводской функции",
            "Ошибка при пустом asset_code",
            "Вставка прошла без ошибки",
            negativeSmoke.TestPlantInsertValidationFails);

        await runner.RunAndPrintAsync();
    }

    private static async Task CommitTxAsync(IStorageTransaction tx)
    {
        await tx.CommitAsync(CancellationToken.None);
    }


    private static KernelAdapter CreateKernel(DatabaseProfile profile = DatabaseProfile.Central) => TestEnvironment.CreateKernel(profile);

    private static StorageConfig LoadStorageConfig() => TestEnvironment.LoadStorageConfig();
    private static async Task<bool> HandleCliAsync(string[] args)
    {
        // Simple subcommand parser
        var cmd = args[0];
        switch (cmd)
        {
            case "add-asset":
                return await CmdAddAssetAsync(args.Skip(1).ToArray());
            case "add-measurements-anpz":
                return await CmdAddMeasurementsAnpzAsync(args.Skip(1).ToArray());
            case "summary":
                return await CmdSummaryAsync(args.Skip(1).ToArray());
            case "top-by-cr":
                return await CmdTopByCrAsync(args.Skip(1).ToArray());
            case "eval-risk":
                return await CmdEvalRiskAsync(args.Skip(1).ToArray());
            case "plant-cr":
                return await CmdPlantCrAsync(args.Skip(1).ToArray());
            case "--help":
            case "-h":
            default:
                PrintUsage();
                return cmd == "--help" || cmd == "-h";
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Использование:");
        Console.WriteLine("  add-asset --id <текст> --name <текст> --plant <текст>");
        Console.WriteLine("  add-measurements-anpz --file <путь csv|json>");
        Console.WriteLine("  summary --plant <текст> [--asset <id>] [--policy <имя>]");
        Console.WriteLine("  top-by-cr --plant <текст> --take <N>");
        Console.WriteLine("  eval-risk --asset <id> [--policy <имя>]");
        Console.WriteLine("  plant-cr --plant <текст> --from <гггг-мм-дд> --to <гггг-мм-дд>");
        Console.WriteLine();
    }

    private static async Task<bool> CmdAddAssetAsync(string[] args)
    {
        string? id = null, name = null, plant = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--id": id = args.ElementAtOrDefault(++i); break;
                case "--name": name = args.ElementAtOrDefault(++i); break;
                case "--plant": plant = args.ElementAtOrDefault(++i); break;
            }
        }
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(plant))
        {
            Console.WriteLine("ошибка: missing required arguments. See --help");
            return false;
        }

        try
        {
            var kernel = CreateKernel();
            var svc = new FnAssetUpsertService(kernel.Storage);
            var rows = await svc.fn_asset_upsertAsync(id!, name!, null, plant!, CancellationToken.None);
            Console.WriteLine($"ок: строк={rows}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ошибка: {ex.Message}");
            return false;
        }
    }

    private sealed record CsvPoint(string AssetCode, string Label, DateTime Ts, decimal Thickness, string? Note, string? Plant);

    private static async Task<bool> CmdAddMeasurementsAnpzAsync(string[] args)
    {
        string? file = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--file") file = args.ElementAtOrDefault(++i);
        }
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
        {
            Console.WriteLine("ошибка: --file is required and must exist");
            return false;
        }

        try
        {
            var (assetCode, sourcePlant, pointsJson) = MeasurementBatchHelper.ParseFile(file);

            var kernel = CreateKernel(DatabaseProfile.PlantAnpz);
            var svc = new SpInsertMeasurementBatchService(kernel.Storage);
            var rows = await svc.sp_insert_measurement_batchAsync(assetCode, pointsJson, sourcePlant, CancellationToken.None);
            Console.WriteLine($"ок: строк={rows} json={Truncate(pointsJson, 200)}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ошибка: {ex.Message}");
            return false;
        }
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "...";

    private static async Task<bool> CmdSummaryAsync(string[] args)
    {
        string? plant = null, asset = null, policy = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--plant": plant = args.ElementAtOrDefault(++i); break;
                case "--asset": asset = args.ElementAtOrDefault(++i); break;
                case "--policy": policy = args.ElementAtOrDefault(++i); break;
            }
        }

        if (string.IsNullOrWhiteSpace(asset))
        {
            Console.WriteLine("ошибка: --asset is required (SQL requires asset_code). --plant is currently informational only.");
            return false;
        }

        try
        {
            var kernel = CreateKernel();
            var svc = new FnAssetSummaryJsonService(kernel.Storage);
            var dto = await svc.fn_asset_summary_jsonAsync(asset!, string.IsNullOrWhiteSpace(policy) ? null : policy, CancellationToken.None);
            if (dto == null)
            {
                Console.WriteLine("ок: пусто");
                return true;
            }
            // Render table from DTO
            Console.WriteLine("ок:");
            Console.WriteLine($"код актива:  {dto.Asset.AssetCode}");
            Console.WriteLine($"название:    {dto.Asset.Name}");
            Console.WriteLine($"тип:         {dto.Asset.Type}");
            Console.WriteLine($"код завода:  {dto.Asset.PlantCode}");
            if (dto.Analytics is not null)
            {
                Console.WriteLine($"пред. толщина: {dto.Analytics.PrevThk}");
                Console.WriteLine($"пред. дата:    {dto.Analytics.PrevDate:O}");
                Console.WriteLine($"посл. толщина: {dto.Analytics.LastThk}");
                Console.WriteLine($"посл. дата:    {dto.Analytics.LastDate:O}");
                Console.WriteLine($"CR:            {dto.Analytics.Cr}");
                Console.WriteLine($"обновлено:     {dto.Analytics.UpdatedAt:O}");
            }
            if (dto.Risk is not null)
            {
                Console.WriteLine($"уровень риска:  {dto.Risk.Level}");
                Console.WriteLine($"порог LOW:      {dto.Risk.ThresholdLow}");
                Console.WriteLine($"порог MED:      {dto.Risk.ThresholdMed}");
                Console.WriteLine($"порог HIGH:     {dto.Risk.ThresholdHigh}");
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ошибка: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> CmdTopByCrAsync(string[] args)
    {
        string? plant = null; int take = 50;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--plant": plant = args.ElementAtOrDefault(++i); break;
                case "--take": int.TryParse(args.ElementAtOrDefault(++i), out take); break;
            }
        }
        try
        {
            var kernel = CreateKernel();
            var svc = new FnTopAssetsByCrService(kernel.Storage);
            var rows = await svc.fn_top_assets_by_crAsync(take, CancellationToken.None);
            Console.WriteLine($"ок: строк={rows.Count}");
            // Render table
            if (rows.Count > 0)
            {
                foreach (var row in rows)
                {
                    Console.WriteLine($"{row.AssetCode,-12} cr={row.Cr} updated={row.UpdatedAt}");
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ошибка: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> CmdEvalRiskAsync(string[] args)
    {
        string? asset = null, policy = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--asset": asset = args.ElementAtOrDefault(++i); break;
                case "--policy": policy = args.ElementAtOrDefault(++i); break;
            }
        }
        if (string.IsNullOrWhiteSpace(asset))
        {
            Console.WriteLine("ошибка: --asset is required");
            return false;
        }
        try
        {
            var kernel = CreateKernel();
            var svc = new FnEvalRiskService(kernel.Storage);
            var rows = await svc.fn_eval_riskAsync(asset!, string.IsNullOrWhiteSpace(policy) ? null : policy, CancellationToken.None);
            Console.WriteLine($"ок: строк={rows.Count}");
            if (rows.Count > 0)
            {
                foreach (var row in rows)
                {
                    Console.WriteLine($"{row.AssetCode,-12} cr={row.Cr} level={row.Level} thresholds={row.ThresholdLow}/{row.ThresholdMed}/{row.ThresholdHigh}");
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ошибка: {ex.Message}");
            return false;
        }
    }

    private static void PrintTable(IReadOnlyList<Dictionary<string, object?>> rows, string[]? columns = null)
    {
        if (rows.Count == 0)
        {
            Console.WriteLine("(пусто)");
            return;
        }

        // Resolve columns list
        if (columns == null)
        {
            var first = rows.First();
            columns = first.Keys.ToList().ToArray();
        }

        // Compute column widths
        var widths = new int[columns.Length];
        for (int i = 0; i < columns.Length; i++)
        {
            var col = columns[i];
            var w = col.Length;
            foreach (var r in rows)
            {
                r.TryGetValue(col, out var v);
                var s = v is DateTime dt ? dt.ToString("u") : v?.ToString() ?? string.Empty;
                if (s.Length > w) w = s.Length;
            }
            widths[i] = w;
        }

        // Header
        for (int i = 0; i < columns.Length; i++)
        {
            var text = columns[i].PadRight(widths[i]);
            Console.Write(i == 0 ? text : "  " + text);
        }
        Console.WriteLine();

        // Rows
        foreach (var r in rows)
        {
            for (int i = 0; i < columns.Length; i++)
            {
                r.TryGetValue(columns[i], out var v);
                var s = v is DateTime dt ? dt.ToString("u") : v?.ToString() ?? string.Empty;
                if (s.Length > widths[i]) s = s.Substring(0, widths[i]);
                Console.Write(i == 0 ? s.PadRight(widths[i]) : "  " + s.PadRight(widths[i]));
            }
            Console.WriteLine();
        }
    }

    private static async Task<bool> CmdPlantCrAsync(string[] args)
    {
        string? plant = null, fromStr = null, toStr = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--plant": plant = args.ElementAtOrDefault(++i); break;
                case "--from": fromStr = args.ElementAtOrDefault(++i); break;
                case "--to": toStr = args.ElementAtOrDefault(++i); break;
            }
        }
        if (string.IsNullOrWhiteSpace(plant) || string.IsNullOrWhiteSpace(fromStr) || string.IsNullOrWhiteSpace(toStr))
        {
            Console.WriteLine("ошибка: --plant, --from, --to are required");
            return false;
        }
        if (!DateTime.TryParse(fromStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var from))
        {
            Console.WriteLine("ошибка: invalid --from");
            return false;
        }
        if (!DateTime.TryParse(toStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var to))
        {
            Console.WriteLine("ошибка: invalid --to");
            return false;
        }
        try
        {
            var kernel = CreateKernel();
            var svc = new PlantCrService(kernel.Storage);
            var dto = await svc.GetPlantCrAsync(plant!, from, to, CancellationToken.None);
            Console.WriteLine("ок:");
            Console.WriteLine($"завод:            {dto.Plant}");
            Console.WriteLine($"интервал с:       {dto.From:O}");
            Console.WriteLine($"интервал по:      {dto.To:O}");
            Console.WriteLine($"CR (среднее):     {dto.CrMean}");
            Console.WriteLine($"CR (P90):         {dto.CrP90}");
            Console.WriteLine($"активов учтено:   {dto.AssetsConsidered}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ошибка: {ex.Message}");
            return false;
        }
    }

}

internal static class TestEnvironment
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<DatabaseProfile, StorageConfig> CachedConfigs = new();

    public static StorageConfig LoadStorageConfig(DatabaseProfile profile = DatabaseProfile.Central)
    {
        lock (SyncRoot)
        {
            if (CachedConfigs.TryGetValue(profile, out var cached)) return cached;
            var resolved = ResolveFromAppSettings(profile)
                           ?? StorageConfigProvider.GetConfig(profile);
            CachedConfigs[profile] = resolved;
            return resolved;
        }
    }

    public static KernelAdapter CreateKernel(DatabaseProfile profile = DatabaseProfile.Central)
    {
        var storage = CreateStorageAdapter(profile);
        return new KernelAdapter(storage);
    }

    public static StorageAdapter CreateStorageAdapter(DatabaseProfile profile = DatabaseProfile.Central) => new StorageAdapter(LoadStorageConfig(profile));

    public static string ConnectionString => LoadStorageConfig().ConnectionString;

    private static StorageConfig? ResolveFromAppSettings(DatabaseProfile profile)
    {
        foreach (var name in new[] { "appsettings.Development.json", "appsettings.json" })
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, name);
                if (!File.Exists(path)) continue;
                using var fs = File.OpenRead(path);
                using var doc = JsonDocument.Parse(fs);
                if (doc.RootElement.TryGetProperty("OILERP", out var oilerp)
                    && oilerp.TryGetProperty("DB", out var db))
                {
                    var suffix = profile switch
                    {
                        DatabaseProfile.PlantAnpz => "_ANPZ",
                        DatabaseProfile.PlantKrnpz => "_KRNPZ",
                        _ => string.Empty
                    };
                    var connProp = string.IsNullOrWhiteSpace(suffix) ? "CONN" : $"CONN{suffix}";
                    var conn = db.TryGetProperty(connProp, out var cEl) ? cEl.GetString() : null;
                    var timeout = db.TryGetProperty("TIMEOUT_SEC", out var tEl) ? tEl.GetInt32() : 30;
                    if (!string.IsNullOrWhiteSpace(conn))
                        return new StorageConfig(conn!, timeout);
                }
            }
            catch
            {
                // Swallow and fallback to defaults
            }
        }

        return null;
    }
}
