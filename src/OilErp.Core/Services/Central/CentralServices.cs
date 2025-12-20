using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Central;

/// <summary>Обертка над public.fn_calc_cr.</summary>
public class FnCalcCrService : AppServiceBase
{
    public FnCalcCrService(IStoragePort storage) : base(storage) { }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> fn_calc_crAsync(
        decimal prev_thk,
        DateTime prev_date,
        decimal last_thk,
        DateTime last_date,
        CancellationToken ct = default)
    {
        var spec = new QuerySpec(
            OperationNames.Central.CalcCr,
            new Dictionary<string, object?>
            {
                ["prev_thk"] = prev_thk,
                ["prev_date"] = prev_date,
                ["last_thk"] = last_thk,
                ["last_date"] = last_date,
            }
        );
        return await Storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec, ct);
    }
}

/// <summary>Обертка над public.fn_asset_upsert.</summary>
public class FnAssetUpsertService : AppServiceBase
{
    public FnAssetUpsertService(IStoragePort storage) : base(storage) { }

    public async Task<int> fn_asset_upsertAsync(
        string p_asset_code,
        string? p_name,
        string? p_type,
        string? p_plant_code,
        CancellationToken ct = default)
    {
        p_asset_code = NormalizeCode(p_asset_code);
        p_name = NormalizeOptional(p_name);
        p_type = NormalizeOptional(p_type);
        p_plant_code = NormalizePlant(p_plant_code);

        var spec = new CommandSpec(
            OperationNames.Central.AssetUpsert,
            new Dictionary<string, object?>
            {
                ["p_asset_code"] = p_asset_code,
                ["p_name"] = p_name,
                ["p_type"] = p_type,
                ["p_plant_code"] = p_plant_code,
            }
        );
        return await Storage.ExecuteCommandAsync(spec, ct);
    }
}

/// <summary>Обертка над public.fn_policy_upsert.</summary>
public class FnPolicyUpsertService : AppServiceBase
{
    public FnPolicyUpsertService(IStoragePort storage) : base(storage) { }

    public async Task<int> fn_policy_upsertAsync(
        string p_name,
        decimal p_low,
        decimal p_med,
        decimal p_high,
        CancellationToken ct = default)
    {
        var spec = new CommandSpec(
            OperationNames.Central.PolicyUpsert,
            new Dictionary<string, object?>
            {
                ["p_name"] = NormalizeCode(p_name),
                ["p_low"] = p_low,
                ["p_med"] = p_med,
                ["p_high"] = p_high
            });
        return await Storage.ExecuteCommandAsync(spec, ct);
    }
}

/// <summary>Обертка над public.fn_eval_risk.</summary>
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
                ["p_asset_code"] = NormalizeCode(p_asset_code),
                ["p_policy_name"] = NormalizeOptional(p_policy_name)
            });
        var rows = await Storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec, ct);
        return rows
            .Select(static r => new EvalRiskRowDto(
                ReadString(r, "asset_code") ?? string.Empty,
                TryDecimal(r, "cr"),
                ReadString(r, "level"),
                TryDecimal(r, "threshold_low"),
                TryDecimal(r, "threshold_med"),
                TryDecimal(r, "threshold_high")))
            .ToList();
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (row.TryGetValue(key, out var v) && v is not null) return v.ToString();
        var match = row.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        return match.Value?.ToString();
    }

    private static decimal? TryDecimal(IReadOnlyDictionary<string, object?> row, string key)
    {
        var s = ReadString(row, key);
        return decimal.TryParse(s, out var d) ? d : null;
    }
}

/// <summary>Обертка над public.fn_asset_summary_json.</summary>
public class FnAssetSummaryJsonService : AppServiceBase
{
    public FnAssetSummaryJsonService(IStoragePort storage) : base(storage) { }

    public async Task<AssetSummaryDto?> fn_asset_summary_jsonAsync(
        string p_asset_code,
        string? p_policy_name,
        CancellationToken ct = default)
    {
        var spec = new QuerySpec(
            OperationNames.Central.AnalyticsAssetSummary,
            new Dictionary<string, object?>
            {
                ["p_asset_code"] = p_asset_code,
                ["p_policy_name"] = p_policy_name,
            }
        );
        var json = (await Storage.ExecuteQueryAsync<string>(spec, ct)).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<AssetSummaryDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}

/// <summary>Обертка над public.fn_top_assets_by_cr.</summary>
public class FnTopAssetsByCrService : AppServiceBase
{
    public FnTopAssetsByCrService(IStoragePort storage) : base(storage) { }

    public async Task<IReadOnlyList<TopAssetCrDto>> fn_top_assets_by_crAsync(
        int p_limit,
        CancellationToken ct = default)
    {
        var spec = new QuerySpec(
            OperationNames.Central.AnalyticsTopAssetsByCr,
            new Dictionary<string, object?>
            {
                ["p_limit"] = p_limit,
            }
        );
        var rows = await Storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec, ct);
        return rows
            .Select(static r => new TopAssetCrDto
            {
                AssetCode = ReadString(r, "asset_code", "asset"),
                Cr = TryDecimal(r, "cr"),
                UpdatedAt = TryDateTime(r, "updated_at")
            })
            .Where(x => x.AssetCode is not null)
            .ToList();
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> row, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (row.TryGetValue(k, out var v) && v is not null) return v.ToString();
            var kvp = row.FirstOrDefault(x => string.Equals(x.Key, k, StringComparison.OrdinalIgnoreCase));
            if (kvp.Value is not null) return kvp.Value.ToString();
        }
        return null;
    }

    private static decimal? TryDecimal(IReadOnlyDictionary<string, object?> row, params string[] keys)
    {
        var s = ReadString(row, keys);
        return decimal.TryParse(s, out var d) ? d : null;
    }

    private static DateTime? TryDateTime(IReadOnlyDictionary<string, object?> row, params string[] keys)
    {
        var s = ReadString(row, keys);
        return DateTime.TryParse(s, out var dt) ? dt : null;
    }
}

/// <summary>Обертка над public.fn_plant_cr_stats.</summary>
public class FnPlantCrStatsService : AppServiceBase
{
    public FnPlantCrStatsService(IStoragePort storage) : base(storage) { }

    public async Task<IReadOnlyList<PlantCrStatDto>> fn_plant_cr_statsAsync(
        string plant,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        var spec = new QuerySpec(
            OperationNames.Central.AnalyticsPlantCrStats,
            new Dictionary<string, object?>
            {
                ["p_plant"] = plant,
                ["p_from"] = from,
                ["p_to"] = to
            });
        var rows = await Storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec, ct);
        return rows
            .Select(static r => new PlantCrStatDto(
                TryDecimal(r, "cr_mean"),
                TryDecimal(r, "cr_p90"),
                TryInt(r, "assets_count")))
            .ToList();
    }

    private static decimal? TryDecimal(IReadOnlyDictionary<string, object?> row, string name)
    {
        if (!row.TryGetValue(name, out var v) || v is null) return null;
        try { return Convert.ToDecimal(v); } catch { return null; }
    }

    private static int TryInt(IReadOnlyDictionary<string, object?> row, string name)
    {
        if (!row.TryGetValue(name, out var v) || v is null) return 0;
        try { return Convert.ToInt32(v); } catch { return 0; }
    }
}

/// <summary>Вызов процедуры public.sp_policy_upsert.</summary>
public class SpPolicyUpsertService : AppServiceBase
{
    public SpPolicyUpsertService(IStoragePort storage) : base(storage) { }

    public async Task<int> sp_policy_upsertAsync(
        string p_name,
        decimal p_low,
        decimal p_med,
        decimal p_high,
        CancellationToken ct = default)
    {
        var spec = new CommandSpec(
            OperationNames.Central.SpPolicyUpsert,
            new Dictionary<string, object?>
            {
                ["p_name"] = NormalizeCode(p_name),
                ["p_low"] = p_low,
                ["p_med"] = p_med,
                ["p_high"] = p_high
            });
        return await Storage.ExecuteCommandAsync(spec, ct);
    }
}

/// <summary>Вызов процедуры public.sp_asset_upsert.</summary>
public class SpAssetUpsertService : AppServiceBase
{
    public SpAssetUpsertService(IStoragePort storage) : base(storage) { }

    public async Task<int> sp_asset_upsertAsync(
        string p_asset_code,
        string? p_name,
        string? p_type,
        string? p_plant_code,
        CancellationToken ct = default)
    {
        p_asset_code = NormalizeCode(p_asset_code);
        p_name = NormalizeOptional(p_name);
        p_type = NormalizeOptional(p_type);
        p_plant_code = NormalizePlant(p_plant_code);

        var spec = new CommandSpec(
            OperationNames.Central.SpAssetUpsert,
            new Dictionary<string, object?>
            {
                ["p_asset_code"] = p_asset_code,
                ["p_name"] = p_name,
                ["p_type"] = p_type,
                ["p_plant_code"] = p_plant_code,
            }
        );
        return await Storage.ExecuteCommandAsync(spec, ct);
    }
}
