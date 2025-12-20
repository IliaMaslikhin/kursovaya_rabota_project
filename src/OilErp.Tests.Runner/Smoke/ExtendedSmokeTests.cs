using System.Globalization;
using System.Text.Json;
using OilErp.Core.Dto;
using OilErp.Core.Operations;
using OilErp.Tests.Runner;
using OilErp.Tests.Runner.Util;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// Расширенные смоук-тесты: аналитика и JSON-контракты.
/// </summary>
public class ExtendedSmokeTests
{
    /// <summary>
    /// Проверяет, что функция fn_calc_cr даёт то же число, что и локальная формула расчёта.
    /// </summary>
    public async Task<TestResult> TestCalcCrFunctionMatchesLocalFormula()
    {
        const string testName = "CalcCr_Function_Matches_Local";
        try
        {
            var storage = TestEnvironment.CreateStorageAdapter();
            var dataSet = HealthCheckDataSet.CreateDefault();
            foreach (var seed in dataSet.Seeds)
            {
                var expected = CalculateCorrosionRate(seed);
                var spec = new QuerySpec(
                    OperationNames.Central.CalcCr,
                    new Dictionary<string, object?>
                    {
                        ["prev_thk"] = seed.PrevThickness,
                        ["prev_date"] = seed.PrevDateUtc,
                        ["last_thk"] = seed.LastThickness,
                        ["last_date"] = seed.LastDateUtc
                    });

                var rows = await storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec);
                var actual = ExtractDecimal(rows.FirstOrDefault(), "fn_calc_cr");
                if (!IsClose(expected, actual))
                {
                    return new TestResult(testName, false, $"Asset {seed.AssetCode}: expected {expected:F4} actual {actual:F4}");
                }
            }

            return new TestResult(testName, true);
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }

    /// <summary>
    /// Проверяет, что fn_eval_risk считает уровни риска по порогам политики.
    /// </summary>
    public async Task<TestResult> TestEvalRiskLevelsAlignWithPolicy()
    {
        const string testName = "EvalRisk_Levels_Align_With_Policy";
        try
        {
            var storage = TestEnvironment.CreateStorageAdapter();
            var dataSet = HealthCheckDataSet.CreateDefault();
            var scenario = new CentralHealthCheckScenario(storage, TestEnvironment.ConnectionString, dataSet);
            await using var seedContext = await scenario.SeedAsync(CancellationToken.None);
            var snapshot = seedContext.Snapshot;

            var errors = new List<string>();
            foreach (var expectation in snapshot.Expectations)
            {
                var spec = new QuerySpec(
                    OperationNames.Central.EvalRisk,
                    new Dictionary<string, object?>
                    {
                        ["p_asset_code"] = expectation.Seed.AssetCode,
                        ["p_policy_name"] = snapshot.PolicyName
                    });

                var rows = await storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec);
                var row = rows.FirstOrDefault();
                if (row == null)
                {
                    errors.Add($"Asset {expectation.Seed.AssetCode}: fn_eval_risk returned no rows");
                    continue;
                }

                var actualLevel = row.TryGetValue("level", out var level) ? level?.ToString() : null;
                if (!string.Equals(actualLevel, expectation.ExpectedRiskLevel, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Asset {expectation.Seed.AssetCode}: expected {expectation.ExpectedRiskLevel} but fn_eval_risk returned {actualLevel ?? "NULL"}");
                }

                if (!CheckThreshold(row, "threshold_low", dataSet.ThresholdLow) ||
                    !CheckThreshold(row, "threshold_med", dataSet.ThresholdMed) ||
                    !CheckThreshold(row, "threshold_high", dataSet.ThresholdHigh))
                {
                    errors.Add($"Asset {expectation.Seed.AssetCode}: threshold mismatch (expected {dataSet.ThresholdLow}/{dataSet.ThresholdMed}/{dataSet.ThresholdHigh})");
                }
            }

            if (errors.Count > 0)
            {
                return new TestResult(testName, false, string.Join("; ", errors));
            }

            return new TestResult(testName, true);
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }

    /// <summary>
    /// Проверяет, что fn_asset_summary_json возвращает нужные блоки (asset/analytics/risk) для каждого актива.
    /// </summary>
    public async Task<TestResult> TestAssetSummaryJsonCompleteness()
    {
        const string testName = "Asset_Summary_Json_Completeness";
        try
        {
            var storage = TestEnvironment.CreateStorageAdapter();
            var dataSet = HealthCheckDataSet.CreateDefault();
            var scenario = new CentralHealthCheckScenario(storage, TestEnvironment.ConnectionString, dataSet);
            await using var seedContext = await scenario.SeedAsync(CancellationToken.None);
            var snapshot = seedContext.Snapshot;

            var issues = new List<string>();
            foreach (var expectation in snapshot.Expectations)
            {
                var spec = new QuerySpec(
                    OperationNames.Central.AnalyticsAssetSummary,
                    new Dictionary<string, object?>
                    {
                        ["p_asset_code"] = expectation.Seed.AssetCode,
                        ["p_policy_name"] = snapshot.PolicyName
                    });

                var rows = await storage.ExecuteQueryAsync<string>(spec);
                var json = rows.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(json))
                {
                    issues.Add($"Asset {expectation.Seed.AssetCode}: summary json is empty");
                    continue;
                }

                if (!ValidateSummary(json, expectation, out var validationError))
                {
                    issues.Add($"Asset {expectation.Seed.AssetCode}: {validationError}");
                }

                Console.WriteLine($"[JSON сводки] {expectation.Seed.AssetCode}: {json}");
            }

            if (issues.Count > 0)
            {
                return new TestResult(testName, false, string.Join("; ", issues));
            }

            return new TestResult(testName, true);
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }

    private static bool ValidateSummary(string json, AssetExpectation expectation, out string error)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("asset", out var asset) || !asset.TryGetProperty("asset_code", out var assetCodeProp))
        {
            error = "asset block missing";
            return false;
        }

        var assetCode = assetCodeProp.GetString();
        if (!string.Equals(assetCode, expectation.Seed.AssetCode, StringComparison.Ordinal))
        {
            error = $"asset_code mismatch (expected {expectation.Seed.AssetCode}, got {assetCode})";
            return false;
        }

        if (!root.TryGetProperty("analytics", out var analytics))
        {
            error = "analytics block missing";
            return false;
        }

        var lastThk = analytics.TryGetProperty("last_thk", out var lastThkEl) ? lastThkEl.GetDecimal() : (decimal?)null;
        if (lastThk == null || Math.Abs(lastThk.Value - expectation.Seed.LastThickness) > 0.0001m)
        {
            error = "analytics.last_thk mismatch";
            return false;
        }

        if (!root.TryGetProperty("risk", out var risk) || !risk.TryGetProperty("level", out var riskLevelProp))
        {
            error = "risk block missing";
            return false;
        }

        var riskLevel = riskLevelProp.GetString();
        if (!string.Equals(riskLevel, expectation.ExpectedRiskLevel, StringComparison.OrdinalIgnoreCase))
        {
            error = $"risk level mismatch (expected {expectation.ExpectedRiskLevel}, got {riskLevel})";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static decimal CalculateCorrosionRate(AssetMeasurementSeed seed)
    {
        var span = seed.LastDateUtc - seed.PrevDateUtc;
        var days = (decimal)Math.Max(1.0, span.TotalDays);
        var delta = seed.PrevThickness - seed.LastThickness;
        return decimal.Round(delta / days, 6, MidpointRounding.AwayFromZero);
    }

    private static decimal ExtractDecimal(Dictionary<string, object?>? row, string column)
    {
        if (row == null || !row.TryGetValue(column, out var value))
            throw new InvalidOperationException($"Column {column} not found in fn_calc_cr result");

        return value switch
        {
            decimal d => d,
            double dbl => (decimal)dbl,
            float fl => (decimal)fl,
            int i => i,
            long l => l,
            string s when decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => throw new InvalidOperationException($"Cannot convert {column} value '{value}' to decimal")
        };
    }

    private static bool CheckThreshold(Dictionary<string, object?> row, string column, decimal expected)
    {
        if (!row.TryGetValue(column, out var value) || value == null)
        {
            return false;
        }

        var actual = ExtractDecimal(row, column);
        return Math.Abs(actual - expected) <= 0.0001m;
    }

    private static bool IsClose(decimal expected, decimal actual) =>
        Math.Abs(expected - actual) <= 0.0001m;
}
