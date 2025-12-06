using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Обертка над public.fn_eval_risk. См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
public class FnEvalRiskService : AppServiceBase
{
    public FnEvalRiskService(IStoragePort storage) : base(storage) { }

    public async Task<IReadOnlyList<EvalRiskRowDto>> fn_eval_riskAsync(
        string p_asset_code,
        string? p_policy_name,
        CancellationToken ct = default)
    {
        var spec = new QuerySpec(
            OperationNames.Central.EvalRisk,
            new Dictionary<string, object?>
            {
                ["p_asset_code"] = p_asset_code,
                ["p_policy_name"] = p_policy_name,
            }
        );
        var rows = await Storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec, ct);
        return rows.Select(Map).ToList();
    }

    private static EvalRiskRowDto Map(Dictionary<string, object?> row) =>
        new(
            AssetCode: ReadString(row, "asset_code"),
            Cr: TryDecimal(row, "cr"),
            Level: ReadString(row, "level"),
            ThresholdLow: TryDecimal(row, "threshold_low"),
            ThresholdMed: TryDecimal(row, "threshold_med"),
            ThresholdHigh: TryDecimal(row, "threshold_high"));

    private static string? ReadString(IReadOnlyDictionary<string, object?> row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetValue(name, out var v) && v is not null) return v.ToString();
            var kvp = row.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase));
            if (kvp.Value is not null) return kvp.Value.ToString();
        }
        return null;
    }

    private static decimal? TryDecimal(IReadOnlyDictionary<string, object?> row, params string[] names)
    {
        var s = ReadString(row, names);
        return decimal.TryParse(s, out var d) ? d : null;
    }
}
