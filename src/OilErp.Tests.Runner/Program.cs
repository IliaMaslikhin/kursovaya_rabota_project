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

namespace OilErp.Tests.Runner;

/// <summary>
/// Console application entry point for smoke tests
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length > 0)
        {
            var exit = await HandleCliAsync(args);
            Environment.ExitCode = exit ? 0 : 1;
            return;
        }

        Console.WriteLine("OilErp Extended Smoke Tests");
        Console.WriteLine("============================");
        Console.WriteLine();

        var runner = new TestRunner();
        var kernelSmoke = new KernelSmoke();
        var storageSmoke = new StorageSmoke();
        var extendedSmoke = new ExtendedSmokeTests();
        var asyncSmoke = new AsyncSmokeTests();
        var validationSmoke = new ValidationSmokeTests();

        // Register all test scenarios
        runner.Register("Kernel_Creates", kernelSmoke.TestKernelCreates);
        runner.Register("Kernel_Exposes_Storage", kernelSmoke.TestKernelExposesStorage);
        runner.Register("Storage_Event_Subscribe_Unsubscribe_NoThrow", storageSmoke.TestStorageSubscribeNotifications);
        runner.Register("Db_Query_TopByCr", storageSmoke.TestDbQueryTopByCr);
        runner.Register("Db_Command_Enqueue", storageSmoke.TestDbCommandEnqueue);
        runner.Register("QuerySpec_EmptyParameters_Allowed", extendedSmoke.TestQuerySpecEmptyParametersAllowed);
        runner.Register("CommandSpec_EmptyParameters_Allowed", extendedSmoke.TestCommandSpecEmptyParametersAllowed);
        runner.Register("OperationNames_Unique_And_NonEmpty", extendedSmoke.TestOperationNamesUniqueAndNonEmpty);
        runner.Register("MeasurementPointDto_Constructs_With_Valid_Data", extendedSmoke.TestMeasurementPointDtoConstructsWithValidData);
        runner.Register("IStoragePort_BeginTransaction_Returns_Disposable", extendedSmoke.TestIStoragePortBeginTransactionReturnsDisposable);
        runner.Register("Transaction_Commit_Path_Sets_Flag", extendedSmoke.TestTransactionCommitPathSetsFlag);
        runner.Register("Transaction_Rollback_Path_Sets_Flag", extendedSmoke.TestTransactionRollbackPathSetsFlag);
        runner.Register("Transaction_Rollback_Twice_NoThrow", extendedSmoke.TestTransactionRollbackTwiceNoThrow);
        runner.Register("Transaction_Commit_After_Rollback_Throws", extendedSmoke.TestTransactionCommitAfterRollbackThrows);
        runner.Register("CancellationToken_Propagates_To_Query", asyncSmoke.TestCancellationTokenPropagatesToQuery);
        runner.Register("CancellationToken_Propagates_To_Command", asyncSmoke.TestCancellationTokenPropagatesToCommand);
        runner.Register("Timeout_On_Query_Is_Observed_By_Adapter", asyncSmoke.TestTimeoutOnQueryIsObservedByAdapter);
        runner.Register("Timeout_On_Command_Is_Observed_By_Adapter", asyncSmoke.TestTimeoutOnCommandIsObservedByAdapter);
        runner.Register("Db_Cancellation_On_Query", asyncSmoke.TestDbCancellationOnQuery);
        runner.Register("Db_Timeout_On_Query", asyncSmoke.TestDbTimeoutOnQuery);
        runner.Register("Concurrent_Commands_Counter_Is_Accurate", asyncSmoke.TestConcurrentCommandsCounterIsAccurate);
        runner.Register("Event_Raise_Notified_Delivers_Payload", asyncSmoke.TestEventRaiseNotifiedDeliversPayload);
        runner.Register("NotImplemented_Adapter_Methods_Fail_As_Expected", asyncSmoke.TestNotImplementedAdapterMethodsFailAsExpected);
        runner.Register("Spec_Null_OperationName_Throws_Argument", validationSmoke.TestSpecNullOperationNameThrowsArgument);
        runner.Register("Nested_Savepoint_Emulation_Commit_Then_Rollback_Outer", validationSmoke.TestNestedSavepointEmulationCommitThenRollbackOuter);
        runner.Register("Repeated_Same_Command_Idempotent_In_Fake", validationSmoke.TestRepeatedSameCommandIdempotentInFake);
        runner.Register("Query_Then_Command_Ordering_Captured", validationSmoke.TestQueryThenCommandOrderingCaptured);
        runner.Register("Runner_Summary_Prints_Total_Count", validationSmoke.TestRunnerSummaryPrintsTotalCount);

        await runner.RunAndPrintAsync();
    }

    private static async Task CommitTxAsync(IAsyncDisposable tx)
    {
        var t = tx.GetType();
        var m = t.GetMethod("CommitAsync", new Type[] { typeof(CancellationToken) });
        if (m != null)
        {
            var task = m.Invoke(tx, new object[] { CancellationToken.None }) as Task;
            if (task != null) await task.ConfigureAwait(false);
            return;
        }
        m = t.GetMethod("CommitAsync", Type.EmptyTypes);
        if (m != null)
        {
            var task = m.Invoke(tx, null) as Task;
            if (task != null) await task.ConfigureAwait(false);
        }
    }


    private static async Task<bool> CmdWatchAsync(string[] args)
    {
        string? channel = null;
        int timeoutSec = 60;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--channel": channel = args.ElementAtOrDefault(++i); break;
                case "--timeout-sec": int.TryParse(args.ElementAtOrDefault(++i), out timeoutSec); break;
            }
        }
        if (string.IsNullOrWhiteSpace(channel))
        {
            Console.WriteLine("err: --channel is required");
            return false;
        }

        var kernel = CreateKernel();
        var sa = kernel.Storage as StorageAdapter ?? throw new InvalidOperationException("Storage must be StorageAdapter for LISTEN/UNLISTEN");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(1, timeoutSec)));

        void OnNotified(object? sender, OilErp.Core.Dto.DbNotification n)
        {
            Console.WriteLine($"pid={n.ProcessId} channel={n.Channel} payload={n.Payload}");
        }

        try
        {
            kernel.Storage.Notified += OnNotified;
            await sa.SubscribeAsync(channel, CancellationToken.None);
            Console.WriteLine($"watching channel '{channel}' for {timeoutSec}s ...");
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
            }
            catch (OperationCanceledException) { }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"err: {ex.Message}");
            return false;
        }
        finally
        {
            try { await sa.UnsubscribeAsync(channel!, CancellationToken.None); } catch { }
            kernel.Storage.Notified -= OnNotified;
        }
    }

    private static KernelAdapter CreateKernel()
    {
        var cfg = LoadStorageConfig();
        var storage = new StorageAdapter(cfg);
        return new KernelAdapter(storage);
    }

    private static StorageConfig LoadStorageConfig()
    {
        // ENV: OILERP__DB__CONN, OILERP__DB__TIMEOUT_SEC
        var conn = Environment.GetEnvironmentVariable("OILERP__DB__CONN");
        var timeoutStr = Environment.GetEnvironmentVariable("OILERP__DB__TIMEOUT_SEC");
        if (!string.IsNullOrWhiteSpace(conn))
        {
            var t = int.TryParse(timeoutStr, out var ti) ? ti : 30;
            return new StorageConfig(conn!, t);
        }

        // Files: appsettings.Development.json or appsettings.json (optional)
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
                    var c = db.TryGetProperty("CONN", out var cEl) ? cEl.GetString() : null;
                    var ts = db.TryGetProperty("TIMEOUT_SEC", out var tEl) ? tEl.GetInt32() : 30;
                    if (!string.IsNullOrWhiteSpace(c)) return new StorageConfig(c!, ts);
                }
            }
            catch { }
        }

        // Fallback defaults for local dev
        return new StorageConfig("Host=localhost;Username=postgres;Password=postgres;Database=postgres", 30);
    }
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
            case "events-peek":
                return await CmdEventsPeekAsync(args.Skip(1).ToArray());
            case "events-ingest":
                return await CmdEventsIngestAsync(args.Skip(1).ToArray());
            case "events-requeue":
                return await CmdEventsRequeueAsync(args.Skip(1).ToArray());
            case "events-cleanup":
                return await CmdEventsCleanupAsync(args.Skip(1).ToArray());
            case "summary":
                return await CmdSummaryAsync(args.Skip(1).ToArray());
            case "top-by-cr":
                return await CmdTopByCrAsync(args.Skip(1).ToArray());
            case "eval-risk":
                return await CmdEvalRiskAsync(args.Skip(1).ToArray());
            case "plant-cr":
                return await CmdPlantCrAsync(args.Skip(1).ToArray());
            case "watch":
                return await CmdWatchAsync(args.Skip(1).ToArray());
            case "--help":
            case "-h":
            default:
                PrintUsage();
                return cmd == "--help" || cmd == "-h";
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  add-asset --id <text> --name <text> --plant <text>");
        Console.WriteLine("  add-measurements-anpz --file <path csv|json>");
        Console.WriteLine("  events-peek --limit <N>");
        Console.WriteLine("  events-ingest --max <N>");
        Console.WriteLine("  events-requeue --age-sec <N>");
        Console.WriteLine("  events-cleanup --age-sec <N>");
        Console.WriteLine("  summary --plant <text> [--asset <id>] [--policy <name>]");
        Console.WriteLine("  top-by-cr --plant <text> --take <N>");
        Console.WriteLine("  eval-risk --asset <id> [--policy <name>]");
        Console.WriteLine("  plant-cr --plant <text> --from <yyyy-mm-dd> --to <yyyy-mm-dd>");
        Console.WriteLine("  watch --channel <text> [--timeout-sec <N>]");
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
            Console.WriteLine("err: missing required arguments. See --help");
            return false;
        }

        try
        {
            var kernel = CreateKernel();
            var svc = new FnAssetUpsertService(kernel.Storage);
            var rows = await svc.fn_asset_upsertAsync(id!, name!, null, plant!, CancellationToken.None);
            Console.WriteLine($"ok: rows={rows}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"err: {ex.Message}");
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
            Console.WriteLine("err: --file is required and must exist");
            return false;
        }

        try
        {
            string assetCode;
            string sourcePlant = "ANPZ";
            JsonElement pointsJson;

            if (Path.GetExtension(file).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                using var fs = File.OpenRead(file);
                using var doc = await JsonDocument.ParseAsync(fs);
                var root = doc.RootElement;
                assetCode = root.GetProperty("asset_code").GetString()!;
                if (root.TryGetProperty("source_plant", out var sp) && sp.ValueKind == JsonValueKind.String)
                    sourcePlant = sp.GetString() ?? sourcePlant;
                pointsJson = root.GetProperty("points");
            }
            else
            {
                // CSV: header expected: asset_code,label,ts,thickness,note,source_plant
                var lines = await File.ReadAllLinesAsync(file);
                if (lines.Length < 2) throw new InvalidOperationException("CSV has no data");
                var header = lines[0].Split(',').Select(s => s.Trim()).ToArray();
                int idxAsset = Array.FindIndex(header, h => string.Equals(h, "asset_code", StringComparison.OrdinalIgnoreCase));
                int idxLabel = Array.FindIndex(header, h => string.Equals(h, "label", StringComparison.OrdinalIgnoreCase));
                int idxTs = Array.FindIndex(header, h => string.Equals(h, "ts", StringComparison.OrdinalIgnoreCase));
                int idxThk = Array.FindIndex(header, h => string.Equals(h, "thickness", StringComparison.OrdinalIgnoreCase));
                int idxNote = Array.FindIndex(header, h => string.Equals(h, "note", StringComparison.OrdinalIgnoreCase));
                int idxPlant = Array.FindIndex(header, h => string.Equals(h, "source_plant", StringComparison.OrdinalIgnoreCase));
                if (idxAsset < 0 || idxLabel < 0 || idxTs < 0 || idxThk < 0)
                    throw new InvalidOperationException("CSV header must include asset_code,label,ts,thickness");

                var points = new List<Dictionary<string, object?>>();
                assetCode = string.Empty;
                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var cols = SplitCsv(line);
                    var ac = cols.ElementAtOrDefault(idxAsset) ?? string.Empty;
                    var lbl = cols.ElementAtOrDefault(idxLabel) ?? string.Empty;
                    var tsStr = cols.ElementAtOrDefault(idxTs) ?? string.Empty;
                    var thkStr = cols.ElementAtOrDefault(idxThk) ?? string.Empty;
                    var note = idxNote >= 0 ? cols.ElementAtOrDefault(idxNote) : null;
                    var plant = idxPlant >= 0 ? cols.ElementAtOrDefault(idxPlant) : null;

                    if (string.IsNullOrWhiteSpace(assetCode)) assetCode = ac;
                    if (!string.IsNullOrWhiteSpace(plant)) sourcePlant = plant!;
                    if (!DateTime.TryParse(tsStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var ts))
                        throw new InvalidOperationException($"Invalid ts: {tsStr}");
                    if (!decimal.TryParse(thkStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var thk))
                        throw new InvalidOperationException($"Invalid thickness: {thkStr}");

                    points.Add(new Dictionary<string, object?>
                    {
                        ["label"] = lbl,
                        ["ts"] = ts.ToString("o"),
                        ["thickness"] = thk,
                        ["note"] = string.IsNullOrWhiteSpace(note) ? null : note
                    });
                }
                var json = JsonSerializer.Serialize(points);
                using var doc = JsonDocument.Parse(json);
                pointsJson = doc.RootElement.Clone();
            }

            var kernel = CreateKernel();
            var svc = new SpInsertMeasurementBatchService(kernel.Storage);
            var rows = await svc.sp_insert_measurement_batchAsync(assetCode, pointsJson.GetRawText(), sourcePlant, CancellationToken.None);
            var snippet = pointsJson.ValueKind == JsonValueKind.Array && pointsJson.GetArrayLength() > 0
                ? pointsJson[0].ToString()
                : "[]";
            Console.WriteLine($"ok: rows={rows} json={Truncate(snippet, 200)}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"err: {ex.Message}");
            return false;
        }
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "...";

    private static string[] SplitCsv(string line)
    {
        var res = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (ch == ',' && !inQuotes)
            {
                res.Add(sb.ToString()); sb.Clear();
            }
            else sb.Append(ch);
        }
        res.Add(sb.ToString());
        return res.ToArray();
    }

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
            Console.WriteLine("err: --asset is required (SQL requires asset_code). --plant is currently informational only.");
            return false;
        }

        try
        {
            var kernel = CreateKernel();
            var svc = new FnAssetSummaryJsonService(kernel.Storage);
            var dto = await svc.fn_asset_summary_jsonAsync(asset!, string.IsNullOrWhiteSpace(policy) ? null : policy, CancellationToken.None);
            if (dto == null)
            {
                Console.WriteLine("ok: empty");
                return true;
            }
            // Render table from DTO
            Console.WriteLine("ok:");
            Console.WriteLine($"asset_code: {dto.Asset.AssetCode}");
            Console.WriteLine($"name:       {dto.Asset.Name}");
            Console.WriteLine($"type:       {dto.Asset.Type}");
            Console.WriteLine($"plant_code: {dto.Asset.PlantCode}");
            if (dto.Analytics is not null)
            {
                Console.WriteLine($"prev_thk:   {dto.Analytics.PrevThk}");
                Console.WriteLine($"prev_date:  {dto.Analytics.PrevDate:O}");
                Console.WriteLine($"last_thk:   {dto.Analytics.LastThk}");
                Console.WriteLine($"last_date:  {dto.Analytics.LastDate:O}");
                Console.WriteLine($"cr:         {dto.Analytics.Cr}");
                Console.WriteLine($"updated_at: {dto.Analytics.UpdatedAt:O}");
            }
            if (dto.Risk is not null)
            {
                Console.WriteLine($"risk.level: {dto.Risk.Level}");
                Console.WriteLine($"risk.low:   {dto.Risk.ThresholdLow}");
                Console.WriteLine($"risk.med:   {dto.Risk.ThresholdMed}");
                Console.WriteLine($"risk.high:  {dto.Risk.ThresholdHigh}");
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"err: {ex.Message}");
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
            Console.WriteLine($"ok: rows={rows.Count}");
            // Render table
            if (rows.Count > 0)
            {
                PrintTable(rows, new[] { "asset_code", "cr", "updated_at" });
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"err: {ex.Message}");
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
            Console.WriteLine("err: --asset is required");
            return false;
        }
        try
        {
            var kernel = CreateKernel();
            var svc = new FnEvalRiskService(kernel.Storage);
            var rows = await svc.fn_eval_riskAsync(asset!, string.IsNullOrWhiteSpace(policy) ? null : policy, CancellationToken.None);
            Console.WriteLine($"ok: rows={rows.Count}");
            if (rows.Count > 0)
            {
                PrintTable(rows, new[] { "asset_code", "cr", "level", "threshold_low", "threshold_med", "threshold_high" });
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"err: {ex.Message}");
            return false;
        }
    }

    private static void PrintTable(IReadOnlyList<Dictionary<string, object?>> rows, string[]? columns = null)
    {
        if (rows.Count == 0)
        {
            Console.WriteLine("(empty)");
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
            Console.WriteLine("err: --plant, --from, --to are required");
            return false;
        }
        if (!DateTime.TryParse(fromStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var from))
        {
            Console.WriteLine("err: invalid --from");
            return false;
        }
        if (!DateTime.TryParse(toStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var to))
        {
            Console.WriteLine("err: invalid --to");
            return false;
        }
        try
        {
            var kernel = CreateKernel();
            var svc = new PlantCrService(kernel.Storage);
            var dto = await svc.GetPlantCrAsync(plant!, from, to, CancellationToken.None);
            Console.WriteLine("ok:");
            Console.WriteLine($"plant: {dto.Plant}");
            Console.WriteLine($"from:  {dto.From:O}");
            Console.WriteLine($"to:    {dto.To:O}");
            Console.WriteLine($"cr_mean: {dto.CrMean}");
            Console.WriteLine($"cr_p90:  {dto.CrP90}");
            Console.WriteLine($"assets_considered: {dto.AssetsConsidered}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"err: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> CmdEventsPeekAsync(string[] args)
    {
        int limit = 10;
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--limit") int.TryParse(args.ElementAtOrDefault(++i), out limit);

        var kernel = CreateKernel();
        await using var tx = await kernel.Storage.BeginTransactionAsync();
        try
        {
            var svc = new FnEventsPeekService(kernel.Storage);
            var rows = await svc.fn_events_peekAsync(limit, CancellationToken.None);
            Console.WriteLine($"ok: rows={rows.Count}");
            if (rows.Count > 0)
            {
                var first = JsonSerializer.Serialize(rows[0]);
                Console.WriteLine($"json={Truncate(first, 240)}");
            }
            await CommitTxAsync(tx);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"err: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> CmdEventsIngestAsync(string[] args)
    {
        int max = 1000;
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--max") int.TryParse(args.ElementAtOrDefault(++i), out max);

        var kernel = CreateKernel();
        await using var tx = await kernel.Storage.BeginTransactionAsync();
        try
        {
            var svc = new FnIngestEventsService(kernel.Storage);
            var affected = await svc.fn_ingest_eventsAsync(max, CancellationToken.None);
            Console.WriteLine($"ok: rows={affected}");
            await CommitTxAsync(tx);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"err: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> CmdEventsRequeueAsync(string[] args)
    {
        int ageSec = 3600;
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--age-sec") int.TryParse(args.ElementAtOrDefault(++i), out ageSec);

        var kernel = CreateKernel();
        await using var tx = await kernel.Storage.BeginTransactionAsync();
        try
        {
            // Эвристика: берем события старше ageSec среди необработанных (peek), ре-киюим их (idempotent)
            var peek = new FnEventsPeekService(kernel.Storage);
            var rows = await peek.fn_events_peekAsync(int.MaxValue, CancellationToken.None);
            var cutoff = DateTime.UtcNow - TimeSpan.FromSeconds(ageSec);
            var ids = rows
                .Where(r => r.TryGetValue("created_at", out var v) && v is DateTime dt && dt <= cutoff)
                .Select(r => Convert.ToInt64(r["id"]))
                .ToArray();

            var svc = new FnEventsRequeueService(kernel.Storage);
            var affected = await svc.fn_events_requeueAsync(ids, CancellationToken.None);
            Console.WriteLine($"ok: rows={affected} ids={ids.Length}");
            await CommitTxAsync(tx);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"err: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> CmdEventsCleanupAsync(string[] args)
    {
        int ageSec = 30 * 24 * 3600;
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--age-sec") int.TryParse(args.ElementAtOrDefault(++i), out ageSec);

        var kernel = CreateKernel();
        await using var tx = await kernel.Storage.BeginTransactionAsync();
        try
        {
            var svc = new FnEventsCleanupService(kernel.Storage);
            var affected = await svc.fn_events_cleanupAsync(TimeSpan.FromSeconds(ageSec), CancellationToken.None);
            Console.WriteLine($"ok: rows={affected}");
            await CommitTxAsync(tx);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"err: {ex.Message}");
            return false;
        }
    }
}
