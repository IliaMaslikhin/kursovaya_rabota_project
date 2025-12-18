using System.Globalization;
using System.Linq;
using System.Text.Json;
using Npgsql;
using OilErp.Core.Dto;
using OilErp.Core.Operations;
using OilErp.Core.Services.Aggregations;
using OilErp.Infrastructure.Adapters;
using OilErp.Tests.Runner;
using OilErp.Tests.Runner.Util;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// Storage smoke tests that seed a realistic data set and verify analytics end-to-end.
/// </summary>
public class StorageSmoke
{
    private readonly HealthCheckDataSet _dataSet = HealthCheckDataSet.CreateDefault();

    /// <summary>
    /// Seeds bulk data through the event pipeline and validates analytics/risk functions.
    /// </summary>
    public async Task<TestResult> TestCentralBulkDataSeedAndAnalytics()
    {
        const string testName = "Central_Bulk_Data_Seed_And_Analytics";
        try
        {
            var storage = TestEnvironment.CreateStorageAdapter();
            var scenario = new CentralHealthCheckScenario(storage, TestEnvironment.ConnectionString, _dataSet);

            var seedSnapshot = await scenario.SeedAsync(CancellationToken.None);
            var verification = await scenario.VerifyAnalyticsAsync(seedSnapshot, CancellationToken.None);

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
    /// Validates plant CR stats (mean/P90) via SQL aggregation.
    /// </summary>
    public async Task<TestResult> TestPlantCrStatsMatchesSeed()
    {
        const string testName = "Plant_Cr_Stats_Match_Seed";
        try
        {
            var storage = TestEnvironment.CreateStorageAdapter();
            var scenario = new CentralHealthCheckScenario(storage, TestEnvironment.ConnectionString, _dataSet);
            var snapshot = await scenario.SeedAsync(CancellationToken.None);

            var svc = new PlantCrService(storage);
            var from = _dataSet.Seeds.Min(s => s.PrevDateUtc).AddDays(-1);
            var to = DateTime.UtcNow.AddDays(1);

            foreach (var plant in _dataSet.Seeds.Select(s => s.PlantCode).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var expectedList = snapshot.Expectations
                    .Where(e => string.Equals(e.Seed.PlantCode, plant, StringComparison.OrdinalIgnoreCase))
                    .Select(e => e.ExpectedCr)
                    .ToList();
                if (expectedList.Count == 0) continue;

                var dto = await svc.GetPlantCrAsync(plant, from, to, CancellationToken.None);
                var expMean = expectedList.Average();
                var sorted = expectedList.OrderBy(x => x).ToArray();
                var idx = (int)Math.Ceiling(0.9m * sorted.Length) - 1;
                idx = Math.Clamp(idx, 0, sorted.Length - 1);
                var expP90 = sorted[idx];

                if (!IsClose(expMean, dto.CrMean ?? 0) || !IsClose(expP90, dto.CrP90 ?? 0))
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

    private static bool IsClose(decimal expected, decimal actual)
    {
        return Math.Abs(expected - actual) <= 0.001m;
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
        return new HealthCheckDataSet(
            PolicyName: "HC_SMOKE_POLICY",
            ThresholdLow: 0.01m,
            ThresholdMed: 0.05m,
            ThresholdHigh: 0.08m,
            Seeds: new List<AssetMeasurementSeed>
            {
                new("HC_UNIT_OK",   "HC Tank OK",   "ANPZ", now.AddDays(-120), now.AddDays(-1),   12.5m, 12.3m),
                new("HC_UNIT_LOW",  "HC Pipe LOW",  "ANPZ", now.AddDays(-90),  now.AddDays(-2),   14.0m, 13.4m),
                new("HC_UNIT_MED",  "HC Pipe MED",  "KRNPZ", now.AddDays(-45), now.AddDays(-1),   11.5m, 10.0m),
                new("HC_UNIT_HIGH", "HC Drum HIGH", "KRNPZ", now.AddDays(-20), now.AddDays(-1),   15.0m, 12.0m)
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

    public async Task<HealthCheckSeedSnapshot> SeedAsync(CancellationToken ct)
    {
        Console.WriteLine($"[Посев] Политика '{_dataSet.PolicyName}': пороги low={_dataSet.ThresholdLow}, med={_dataSet.ThresholdMed}, high={_dataSet.ThresholdHigh}");
        await UpsertPolicyAsync(ct);

        var expectations = new List<AssetExpectation>(_dataSet.Seeds.Count);
        foreach (var seed in _dataSet.Seeds)
        {
            await UpsertAssetAsync(seed, ct);
            await InsertMeasurementBatchAsync(seed, ct);
            var expectedCr = CalculateCorrosionRate(seed);
            var expectedLevel = DetermineRiskLevel(expectedCr);
            expectations.Add(new AssetExpectation(seed, expectedCr, expectedLevel));
            Console.WriteLine($"[Посев] Актив={seed.AssetCode} пред={seed.PrevThickness}@{seed.PrevDateUtc:O} тек={seed.LastThickness}@{seed.LastDateUtc:O} -> ожидаемый CR={expectedCr:F4} ({expectedLevel})");
        }

        return new HealthCheckSeedSnapshot(_dataSet.PolicyName, expectations);
    }

    public async Task<AnalyticsVerification> VerifyAnalyticsAsync(HealthCheckSeedSnapshot snapshot, CancellationToken ct)
    {
        var topRows = await _storage.ExecuteQueryAsync<Dictionary<string, object?>>(BuildTopAnalyticsSpec(snapshot), ct);
        var topByAsset = topRows
            .Where(r => r.TryGetValue("asset_code", out var code) && code is string)
            .GroupBy(r => (string)r["asset_code"]!)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var outputRows = new List<AssetAnalyticsRow>();
        var errors = new List<string>();
        foreach (var expectation in snapshot.Expectations)
        {
            if (!topByAsset.TryGetValue(expectation.Seed.AssetCode, out var row))
            {
                errors.Add($"Asset {expectation.Seed.AssetCode} is missing in analytics.fn_top_assets_by_cr output");
                continue;
            }

            var actualCr = ReadDecimal(row, "cr");
            if (!IsClose(expectation.ExpectedCr, actualCr))
            {
                errors.Add($"Asset {expectation.Seed.AssetCode} CR mismatch. expected={expectation.ExpectedCr:F4} actual={actualCr:F4}");
            }

            var summary = await FetchSummaryAsync(expectation.Seed.AssetCode, snapshot.PolicyName, ct);
            if (summary is null)
            {
                errors.Add($"Asset {expectation.Seed.AssetCode} summary is null (fn_asset_summary_json returned NULL)");
                continue;
            }

            var actualLevel = summary.RiskLevel ?? "UNKNOWN";
            if (!string.Equals(expectation.ExpectedRiskLevel, actualLevel, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Asset {expectation.Seed.AssetCode} risk mismatch. expected={expectation.ExpectedRiskLevel} actual={actualLevel}");
            }

            var evalLevel = await FetchEvalRiskLevelAsync(expectation.Seed.AssetCode, snapshot.PolicyName, ct);
            if (evalLevel is null)
            {
                errors.Add($"Asset {expectation.Seed.AssetCode} eval risk returned NULL");
            }
            else if (!string.Equals(evalLevel, actualLevel, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Asset {expectation.Seed.AssetCode} eval risk disagrees with summary. eval={evalLevel}, summary={actualLevel}");
            }

            outputRows.Add(new AssetAnalyticsRow(expectation.Seed.AssetCode, expectation.ExpectedCr, actualCr, expectation.ExpectedRiskLevel, actualLevel, summary.UpdatedAt, summary.RawJson));
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

    private QuerySpec BuildTopAnalyticsSpec(HealthCheckSeedSnapshot snapshot)
    {
        return new QuerySpec(
            OperationNames.Central.AnalyticsTopAssetsByCr,
            new Dictionary<string, object?>
            {
                ["p_limit"] = Math.Max(snapshot.Expectations.Count * 5, 50)
            });
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

    private async Task UpsertAssetAsync(AssetMeasurementSeed seed, CancellationToken ct)
    {
        var spec = new CommandSpec(
            OperationNames.Central.AssetUpsert,
            new Dictionary<string, object?>
            {
                ["p_asset_code"] = seed.AssetCode,
                ["p_name"] = seed.DisplayName,
                ["p_type"] = "PROCESS_UNIT",
                ["p_plant_code"] = seed.PlantCode
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

    private static bool IsClose(decimal expected, decimal actual)
    {
        return Math.Abs(expected - actual) <= 0.001m;
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

    private static decimal ReadDecimal(Dictionary<string, object?> row, string column)
    {
        if (!row.TryGetValue(column, out var value))
            throw new InvalidOperationException($"Column '{column}' not found in analytics row");

        return value switch
        {
            decimal d => d,
            double dbl => (decimal)dbl,
            float fl => (decimal)fl,
            int i => i,
            long l => l,
            string s when decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => throw new InvalidOperationException($"Cannot convert '{column}' value '{value}' to decimal")
        };
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
