using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Обертка над public.fn_plant_cr_stats.
/// </summary>
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
