using System.Globalization;
using System.Linq;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using OilErp.Core.Dto;
using OilErp.Core.Operations;
using OilErp.Core.Services.Aggregations;
using OilErp.Core.Contracts;
using OilErp.Infrastructure.Adapters;
using OilErp.Tests.Runner;
using OilErp.Tests.Runner.Util;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// Смоук-тесты хранилища: посев данных и проверка аналитики end-to-end.
/// </summary>
public class StorageSmoke
{
    /// <summary>
    /// Делает посев тестовых данных и проверяет аналитику/риски на этих данных.
    /// </summary>
    public async Task<TestResult> TestCentralBulkDataSeedAndAnalytics()
    {
        const string testName = "Central_Bulk_Data_Seed_And_Analytics";
        try
        {
            var storage = TestEnvironment.CreateStorageAdapter();
            var dataSet = HealthCheckDataSet.CreateDefault();
            var scenario = new CentralHealthCheckScenario(storage, TestEnvironment.ConnectionString, dataSet);

            await using var seedContext = await scenario.SeedAsync(CancellationToken.None);
            var verification = await scenario.VerifyAnalyticsAsync(seedContext.Snapshot, CancellationToken.None);

            if (!verification.Success)
            {
                return new TestResult(testName, false, verification.ErrorMessage);
            }

            scenario.PrintAnalyticsTable(verification.Rows);
            return new TestResult(testName, true);
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }

    /// <summary>
    /// Проверяет статистику CR по заводу (mean/p90) через SQL-функцию.
    /// </summary>
    public async Task<TestResult> TestPlantCrStatsMatchesSeed()
    {
        const string testName = "Plant_Cr_Stats_Match_Seed";
        try
        {
            var storage = TestEnvironment.CreateStorageAdapter();
            var dataSet = HealthCheckDataSet.CreateDefault();
            var scenario = new CentralHealthCheckScenario(storage, TestEnvironment.ConnectionString, dataSet);
            await using var seedContext = await scenario.SeedAsync(CancellationToken.None);
            var snapshot = seedContext.Snapshot;

            var svc = new PlantCrService(storage);
            var from = dataSet.Seeds.Min(s => s.PrevDateUtc).AddDays(-1);
            var to = DateTime.UtcNow.AddDays(1);

            foreach (var plant in dataSet.Seeds.Select(s => s.PlantCode).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var expectedList = snapshot.Expectations
                    .Where(e => string.Equals(e.Seed.PlantCode, plant, StringComparison.OrdinalIgnoreCase))
                    .Select(e => e.ExpectedCr)
                    .ToList();
                if (expectedList.Count == 0) continue;

                var dto = await svc.GetPlantCrAsync(plant, from, to, CancellationToken.None);
                var expMean = expectedList.Average();
                var sorted = expectedList.OrderBy(x => x).ToArray();
                var expP90 = PercentileCont(sorted, 0.9m);

                const decimal tolerance = 0.01m;
                if (!IsClose(expMean, dto.CrMean ?? 0, tolerance) || !IsClose(expP90, dto.CrP90 ?? 0, tolerance))
                {
                    return new TestResult(testName, false,
                        $"Плант {plant}: ожидалось mean={expMean:F4}, p90={expP90:F4}, получено mean={dto.CrMean}, p90={dto.CrP90}");
                }
            }

            return new TestResult(testName, true);
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }

    private static bool IsClose(decimal expected, decimal actual, decimal tolerance = 0.001m)
    {
        var adaptive = Math.Max(tolerance, Math.Abs(expected) * 0.1m); // допускаем до 10% отклонения или фикс. порог
        return Math.Abs(expected - actual) <= adaptive;
    }

    private static decimal PercentileCont(decimal[] sortedAsc, decimal p)
    {
        if (sortedAsc.Length == 0) return 0m;
        if (sortedAsc.Length == 1) return sortedAsc[0];

        if (p < 0m) p = 0m;
        if (p > 1m) p = 1m;

        var pos = (sortedAsc.Length - 1) * p;
        var idx = (int)decimal.Floor(pos);
        var frac = pos - idx;

        if (idx >= sortedAsc.Length - 1) return sortedAsc[^1];
        return sortedAsc[idx] + (sortedAsc[idx + 1] - sortedAsc[idx]) * frac;
    }
}

internal sealed record HealthCheckDataSet(
    string PolicyName,
    decimal ThresholdLow,
    decimal ThresholdMed,
    decimal ThresholdHigh,
    IReadOnlyList<AssetMeasurementSeed> Seeds)
{
    public static HealthCheckDataSet CreateDefault()
    {
        var now = DateTime.UtcNow;
        var runId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var plantAnpz = $"ANPZ_{runId}";
        var plantKrnpz = $"KRNPZ_{runId}";
        return new HealthCheckDataSet(
            PolicyName: $"HC_SMOKE_POLICY_{runId}",
            ThresholdLow: 0.01m,
            ThresholdMed: 0.05m,
            ThresholdHigh: 0.08m,
            Seeds: new List<AssetMeasurementSeed>
            {
                new($"HC_{runId}_UNIT_OK",   $"HC Tank OK ({runId})",   plantAnpz, now.AddDays(-120), now.AddDays(-1),   12.5m, 12.3m),
                new($"HC_{runId}_UNIT_LOW",  $"HC Pipe LOW ({runId})",  plantAnpz, now.AddDays(-90),  now.AddDays(-2),   14.0m, 13.4m),
                new($"HC_{runId}_UNIT_MED",  $"HC Pipe MED ({runId})",  plantKrnpz, now.AddDays(-45), now.AddDays(-1),   11.5m, 10.0m),
                new($"HC_{runId}_UNIT_HIGH", $"HC Drum HIGH ({runId})", plantKrnpz, now.AddDays(-20), now.AddDays(-1),   15.0m, 12.0m)
            });
    }
}

internal sealed record AssetMeasurementSeed(
    string AssetCode,
    string DisplayName,
    string PlantCode,
    DateTime PrevDateUtc,
    DateTime LastDateUtc,
    decimal PrevThickness,
    decimal LastThickness);

internal sealed record AssetExpectation(
    AssetMeasurementSeed Seed,
    decimal ExpectedCr,
    string ExpectedRiskLevel);

internal sealed record AssetAnalyticsRow(
    string AssetCode,
    decimal ExpectedCr,
    decimal ActualCr,
    string ExpectedRiskLevel,
    string ActualRiskLevel,
    DateTime? UpdatedAt,
    string SummaryJson);

internal sealed record HealthCheckSeedSnapshot(string PolicyName, IReadOnlyList<AssetExpectation> Expectations)
{
    public IReadOnlySet<string> AssetCodes => Expectations.Select(e => e.Seed.AssetCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
}

internal sealed record AnalyticsVerification(bool Success, string? ErrorMessage, IReadOnlyList<AssetAnalyticsRow> Rows)
{
    public static AnalyticsVerification Fail(string error) =>
        new(false, error, Array.Empty<AssetAnalyticsRow>());

    public static AnalyticsVerification SuccessResult(IReadOnlyList<AssetAnalyticsRow> rows) =>
        new(true, null, rows);
}

internal sealed record SimpleVerification(bool Success, string? ErrorMessage)
{
    public static SimpleVerification Ok() => new(true, null);
    public static SimpleVerification Fail(string message) => new(false, message);
}

internal sealed class CentralHealthCheckScenario
{
    private readonly StorageAdapter _storage;
    private readonly string _connectionString;
    private readonly HealthCheckDataSet _dataSet;

    public CentralHealthCheckScenario(StorageAdapter storage, string connectionString, HealthCheckDataSet dataSet)
    {
        _storage = storage;
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _dataSet = dataSet;
    }

    public async Task<HealthCheckSeedContext> SeedAsync(CancellationToken ct)
    {
        var assetCodes = _dataSet.Seeds.Select(s => s.AssetCode).ToArray();
        var tx = await _storage.BeginTransactionAsync(ct);
        Console.WriteLine($"[Посев] Политика '{_dataSet.PolicyName}': пороги low={_dataSet.ThresholdLow}, med={_dataSet.ThresholdMed}, high={_dataSet.ThresholdHigh}");
        try
        {
            await UpsertPolicyAsync(ct);

            var expectations = new List<AssetExpectation>(_dataSet.Seeds.Count);
            foreach (var seed in _dataSet.Seeds)
            {
                // Актив создаётся триггером в central при вставке батча (measurement_batches).
                await InsertMeasurementBatchAsync(seed, ct);
                var expectedCr = CalculateCorrosionRate(seed);
                var expectedLevel = DetermineRiskLevel(expectedCr);
                expectations.Add(new AssetExpectation(seed, expectedCr, expectedLevel));
                Console.WriteLine($"[Посев] Актив={seed.AssetCode} пред={seed.PrevThickness}@{seed.PrevDateUtc:O} тек={seed.LastThickness}@{seed.LastDateUtc:O} -> ожидаемый CR={expectedCr:F4} ({expectedLevel})");
            }

            var snapshot = new HealthCheckSeedSnapshot(_dataSet.PolicyName, expectations);
            return new HealthCheckSeedContext(snapshot, tx, _connectionString, assetCodes);
        }
        catch
        {
            try
            {
                await tx.RollbackAsync(ct);
            }
            catch
            {
                // откат мог упасть (например, если связь с БД оборвалась) — всё равно пробуем почистить
            }

            try
            {
                await HealthCheckSeedContext.CleanupArtifactsAsync(_connectionString, _dataSet.PolicyName, assetCodes, ct);
            }
            catch
            {
                // если чистка не удалась — тест всё равно упадёт, но хотя бы не скрываем исходную причину
            }

            try
            {
                await tx.DisposeAsync();
            }
            catch
            {
                // игнорируем освобождение, чтобы не затереть исходную ошибку
            }
            throw;
        }
    }

    public async Task<AnalyticsVerification> VerifyAnalyticsAsync(HealthCheckSeedSnapshot snapshot, CancellationToken ct)
    {
        // Просто проверяем, что функция top-by-cr вообще выполняется.
        // В непустой базе наши тестовые активы могут не попасть в top N, поэтому не привязываемся к выдаче.
        await _storage.ExecuteQueryAsync<Dictionary<string, object?>>(
            new QuerySpec(
                OperationNames.Central.AnalyticsTopAssetsByCr,
                new Dictionary<string, object?> { ["p_limit"] = 1 }),
            ct);

        var outputRows = new List<AssetAnalyticsRow>();
        var errors = new List<string>();
        foreach (var expectation in snapshot.Expectations)
        {
            var (actualCr, updatedAt) = await FetchAnalyticsCrAsync(expectation.Seed.AssetCode, ct);
            if (actualCr == null)
            {
                errors.Add($"Актив {expectation.Seed.AssetCode}: нет строки в public.analytics_cr после вставки батча");
                continue;
            }

            if (!IsClose(expectation.ExpectedCr, actualCr.Value))
            {
                errors.Add($"Актив {expectation.Seed.AssetCode}: CR не совпал (ожидали {expectation.ExpectedCr:F4}, получили {actualCr.Value:F4})");
            }

            var summary = await FetchSummaryAsync(expectation.Seed.AssetCode, snapshot.PolicyName, ct);
            if (summary is null)
            {
                errors.Add($"Актив {expectation.Seed.AssetCode}: fn_asset_summary_json вернула NULL");
                continue;
            }

            var actualLevel = summary.RiskLevel ?? "UNKNOWN";
            if (!string.Equals(expectation.ExpectedRiskLevel, actualLevel, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Актив {expectation.Seed.AssetCode}: уровень риска не совпал (ожидали {expectation.ExpectedRiskLevel}, получили {actualLevel})");
            }

            var evalLevel = await FetchEvalRiskLevelAsync(expectation.Seed.AssetCode, snapshot.PolicyName, ct);
            if (evalLevel is null)
            {
                errors.Add($"Актив {expectation.Seed.AssetCode}: fn_eval_risk вернула NULL");
            }
            else if (!string.Equals(evalLevel, actualLevel, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Актив {expectation.Seed.AssetCode}: fn_eval_risk не совпала с summary (eval={evalLevel}, summary={actualLevel})");
            }

            outputRows.Add(new AssetAnalyticsRow(expectation.Seed.AssetCode, expectation.ExpectedCr, actualCr.Value, expectation.ExpectedRiskLevel, actualLevel, updatedAt ?? summary.UpdatedAt, summary.RawJson));
        }

        if (errors.Count > 0)
        {
            return AnalyticsVerification.Fail(string.Join("; ", errors));
        }

        return AnalyticsVerification.SuccessResult(outputRows);
    }

    public void PrintAnalyticsTable(IEnumerable<AssetAnalyticsRow> rows)
    {
        Console.WriteLine("Актив            | CR ожидаемый | CR фактический | Риск (ож/факт) | Обновлено");
        Console.WriteLine("-----------------|---------------|---------------|-----------------|------------------------------");
        foreach (var row in rows)
        {
            Console.WriteLine(
                $"{row.AssetCode,-16}| {row.ExpectedCr.ToString("F4", CultureInfo.InvariantCulture),11} | " +
                $"{row.ActualCr.ToString("F4", CultureInfo.InvariantCulture),9} | " +
                $"{row.ExpectedRiskLevel}/{row.ActualRiskLevel,-14} | " +
                $"{row.UpdatedAt?.ToString("O", CultureInfo.InvariantCulture) ?? "n/a"}");
        }
        Console.WriteLine();
    }

    private async Task<(decimal? Cr, DateTime? UpdatedAt)> FetchAnalyticsCrAsync(string assetCode, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select cr, updated_at from public.analytics_cr where asset_code = @asset limit 1";
        cmd.Parameters.AddWithValue("@asset", assetCode);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return (null, null);
        var cr = reader.IsDBNull(0) ? (decimal?)null : reader.GetFieldValue<decimal>(0);
        var updated = reader.IsDBNull(1) ? (DateTime?)null : reader.GetFieldValue<DateTime>(1);
        return (cr, updated);
    }

    private async Task UpsertPolicyAsync(CancellationToken ct)
    {
        var spec = new CommandSpec(
            OperationNames.Central.PolicyUpsert,
            new Dictionary<string, object?>
            {
                ["p_name"] = _dataSet.PolicyName,
                ["p_low"] = _dataSet.ThresholdLow,
                ["p_med"] = _dataSet.ThresholdMed,
                ["p_high"] = _dataSet.ThresholdHigh
            });

        await _storage.ExecuteCommandAsync(spec, ct);
    }

    private async Task InsertMeasurementBatchAsync(AssetMeasurementSeed seed, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            insert into public.measurement_batches(source_plant, asset_code, prev_thk, prev_date, last_thk, last_date)
            values (@plant, @asset, @prev_thk, @prev_date, @last_thk, @last_date);
            """;
        cmd.Parameters.AddWithValue("@plant", seed.PlantCode);
        cmd.Parameters.AddWithValue("@asset", seed.AssetCode);
        cmd.Parameters.AddWithValue("@prev_thk", seed.PrevThickness);
        cmd.Parameters.AddWithValue("@prev_date", seed.PrevDateUtc);
        cmd.Parameters.AddWithValue("@last_thk", seed.LastThickness);
        cmd.Parameters.AddWithValue("@last_date", seed.LastDateUtc);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static decimal CalculateCorrosionRate(AssetMeasurementSeed seed)
    {
        var span = seed.LastDateUtc - seed.PrevDateUtc;
        var days = (decimal)Math.Max(1.0, span.TotalDays);
        var delta = seed.PrevThickness - seed.LastThickness;
        return decimal.Round(delta / days, 6, MidpointRounding.AwayFromZero);
    }

    private string DetermineRiskLevel(decimal cr)
    {
        if (cr >= _dataSet.ThresholdHigh) return "HIGH";
        if (cr >= _dataSet.ThresholdMed) return "MEDIUM";
        if (cr >= _dataSet.ThresholdLow) return "LOW";
        return "OK";
    }

    private async Task<AssetSummarySnapshot?> FetchSummaryAsync(string assetCode, string policyName, CancellationToken ct)
    {
        var spec = new QuerySpec(
            OperationNames.Central.AnalyticsAssetSummary,
            new Dictionary<string, object?>
            {
                ["p_asset_code"] = assetCode,
                ["p_policy_name"] = policyName
            });

        var rows = await _storage.ExecuteQueryAsync<string>(spec, ct);
        var json = rows.FirstOrDefault();
        return json == null ? null : AssetSummarySnapshot.FromJson(json);
    }

    private async Task<string?> FetchEvalRiskLevelAsync(string assetCode, string policyName, CancellationToken ct)
    {
        var spec = new QuerySpec(
            OperationNames.Central.EvalRisk,
            new Dictionary<string, object?>
            {
                ["p_asset_code"] = assetCode,
                ["p_policy_name"] = policyName
            });

        var rows = await _storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec, ct);
        var row = rows.FirstOrDefault();
        if (row == null) return null;
        return row.TryGetValue("level", out var level) ? level?.ToString() : null;
    }

    private static bool IsClose(decimal expected, decimal actual, decimal tolerance = 0.001m)
    {
        var adaptive = Math.Max(tolerance, Math.Abs(expected) * 0.1m);
        return Math.Abs(expected - actual) <= adaptive;
    }
}

internal sealed class HealthCheckSeedContext : IAsyncDisposable
{
    public HealthCheckSeedContext(HealthCheckSeedSnapshot snapshot, IStorageTransaction transaction, string connectionString, IReadOnlyList<string> assetCodes)
    {
        Snapshot = snapshot;
        Transaction = transaction;
        ConnectionString = connectionString;
        AssetCodes = assetCodes;
    }

    public HealthCheckSeedSnapshot Snapshot { get; }
    private IStorageTransaction Transaction { get; }
    private string ConnectionString { get; }
    private IReadOnlyList<string> AssetCodes { get; }
    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        try
        {
            await Transaction.RollbackAsync();
            await CleanupArtifactsAsync(ConnectionString, Snapshot.PolicyName, AssetCodes, CancellationToken.None);
        }
        catch
        {
            // игнорируем ошибки отката/чистки, освобождаем ресурсы
        }
        finally
        {
            await Transaction.DisposeAsync();
            _disposed = true;
        }
    }

    internal static async Task CleanupArtifactsAsync(string connectionString, string policyName, IReadOnlyList<string> assetCodes, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || assetCodes.Count == 0) return;
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        var codes = assetCodes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                delete from public.analytics_cr where asset_code = any(@codes);
                delete from public.measurement_batches where asset_code = any(@codes);
                delete from public.assets_global where asset_code = any(@codes);
                """;
            var p = cmd.Parameters.Add("codes", NpgsqlDbType.Array | NpgsqlDbType.Text);
            p.Value = codes;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "delete from public.risk_policies where name = @name";
            cmd.Parameters.AddWithValue("name", policyName);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}

internal sealed record AssetSummarySnapshot(string AssetCode, string? RiskLevel, DateTime? UpdatedAt, string RawJson)
{
    public static AssetSummarySnapshot FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var assetCode = root.GetProperty("asset").GetProperty("asset_code").GetString() ?? "UNKNOWN";
        string? riskLevel = null;
        if (root.TryGetProperty("risk", out var risk) && risk.TryGetProperty("level", out var levelProp))
        {
            riskLevel = levelProp.GetString();
        }
        DateTime? updated = null;
        if (root.TryGetProperty("analytics", out var analytics) &&
            analytics.TryGetProperty("updated_at", out var updatedEl) &&
            updatedEl.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(updatedEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            updated = parsed;
        }

        return new AssetSummarySnapshot(assetCode, riskLevel, updated, json);
    }
}
