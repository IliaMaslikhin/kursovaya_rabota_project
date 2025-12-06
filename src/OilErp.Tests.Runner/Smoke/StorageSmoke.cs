using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
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
            var scenario = new CentralHealthCheckScenario(storage, _dataSet);

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
    /// Ensures that after ingestion there are no dangling events for the seeded assets.
    /// </summary>
    public async Task<TestResult> TestCentralEventQueueIntegrity()
    {
        const string testName = "Central_Event_Queue_Integrity";
        try
        {
            var storage = TestEnvironment.CreateStorageAdapter();
            var scenario = new CentralHealthCheckScenario(storage, _dataSet);
            var seedSnapshot = await scenario.SeedAsync(CancellationToken.None);
            var queueVerification = await scenario.VerifyQueueDrainAsync(seedSnapshot, CancellationToken.None);

            if (!queueVerification.Success)
            {
                return new TestResult(testName, false, queueVerification.ErrorMessage);
            }

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
            var scenario = new CentralHealthCheckScenario(storage, _dataSet);
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
    private readonly HealthCheckDataSet _dataSet;
    private const string EventType = "HC_MEASUREMENT_BATCH";

    public CentralHealthCheckScenario(StorageAdapter storage, HealthCheckDataSet dataSet)
    {
        _storage = storage;
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
            await EnqueueMeasurementAsync(seed, ct);
            var expectedCr = CalculateCorrosionRate(seed);
            var expectedLevel = DetermineRiskLevel(expectedCr);
            expectations.Add(new AssetExpectation(seed, expectedCr, expectedLevel));
            Console.WriteLine($"[Посев] Актив={seed.AssetCode} пред={seed.PrevThickness}@{seed.PrevDateUtc:O} тек={seed.LastThickness}@{seed.LastDateUtc:O} -> ожидаемый CR={expectedCr:F4} ({expectedLevel})");
        }

        await DrainEventsAsync(ct);
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

    public async Task<SimpleVerification> VerifyQueueDrainAsync(HealthCheckSeedSnapshot snapshot, CancellationToken ct)
    {
        var peekSpec = new QuerySpec(
            OperationNames.Central.EventsPeek,
            new Dictionary<string, object?>
            {
                ["p_limit"] = Math.Max(snapshot.Expectations.Count * 5, 20)
            });

        var rows = await _storage.ExecuteQueryAsync<Dictionary<string, object?>>(peekSpec, ct);
        var leftovers = rows
            .Select(r => (Row: r, AssetCode: TryReadAssetCode(r)))
            .Where(tuple => tuple.AssetCode != null && snapshot.AssetCodes.Contains(tuple.AssetCode))
            .ToList();

        if (leftovers.Count == 0)
        {
            return SimpleVerification.Ok();
        }

        var builder = new StringBuilder();
        builder.Append("Unprocessed measurement events remain for assets: ");
        builder.Append(string.Join(", ", leftovers.Select(l =>
        {
            var id = l.Row.TryGetValue("id", out var value) ? value?.ToString() : "?";
            return $"{l.AssetCode} (event #{id})";
        })));
        builder.Append(". TODO: auto-create missing queue cleanup when provisioning is automated.");
        return SimpleVerification.Fail(builder.ToString());
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

    private async Task EnqueueMeasurementAsync(AssetMeasurementSeed seed, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            asset_code = seed.AssetCode,
            prev_thk = seed.PrevThickness,
            prev_date = seed.PrevDateUtc,
            last_thk = seed.LastThickness,
            last_date = seed.LastDateUtc
        });

        var spec = new CommandSpec(
            OperationNames.Central.EventsEnqueue,
            new Dictionary<string, object?>
            {
                ["p_event_type"] = EventType,
                ["p_source_plant"] = seed.PlantCode,
                ["p_payload"] = payload
            });

        await _storage.ExecuteCommandAsync(spec, ct);
    }

    private async Task DrainEventsAsync(CancellationToken ct)
    {
        // Run ingestion several times to ensure all enqueued events are processed (queue might have foreign items).
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var spec = new CommandSpec(
                OperationNames.Central.EventsIngest,
                new Dictionary<string, object?> { ["p_limit"] = 5000 });

            var processed = await _storage.ExecuteCommandAsync(spec, ct);
            Console.WriteLine($"[Посев] Попытка инжеста {attempt + 1}: обработано {processed} событий");
            if (processed == 0) break;
        }
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

    private static string? TryReadAssetCode(Dictionary<string, object?> row)
    {
        if (!row.TryGetValue("payload_json", out var payload) || payload == null)
            return null;

        try
        {
            if (payload is string s)
            {
                using var doc = JsonDocument.Parse(s);
                return doc.RootElement.TryGetProperty("asset_code", out var prop) ? prop.GetString() : null;
            }

            if (payload is JsonElement element)
            {
                return element.TryGetProperty("asset_code", out var prop) ? prop.GetString() : null;
            }
        }
        catch
        {
            return null;
        }

        return null;
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
