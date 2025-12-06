using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Services.Central;
using OilErp.Core.Services.Dtos;

namespace OilErp.Core.Services.Aggregations;

/// <summary>
/// Агрегатор CR по заводу за интервал времени без изменения SQL.
/// Источники данных: public.fn_top_assets_by_cr (для CR и updated_at), public.fn_asset_summary_json (для plant_code по asset_code).
/// Агрегация: арифметическое среднее CR и перцентиль P90 по множеству подходящих активов.
/// </summary>
public class PlantCrService : AppServiceBase
{
    public PlantCrService(IStoragePort storage) : base(storage) { }

    public async Task<PlantCrAggregateDto> GetPlantCrAsync(string plant, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var svc = new FnPlantCrStatsService(Storage);
        var rows = await svc.fn_plant_cr_statsAsync(plant, from, to, ct);
        var row = rows.FirstOrDefault();
        decimal? mean = null, p90 = null;
        int assets = 0;
        if (row != null)
        {
            mean = TryReadDecimal(row, "cr_mean");
            p90 = TryReadDecimal(row, "cr_p90");
            assets = row.TryGetValue("assets_count", out var c) && c is not null ? Convert.ToInt32(c) : 0;
        }
        return new PlantCrAggregateDto(plant, from, to, mean, p90, assets);
    }

    private static decimal? TryReadDecimal(Dictionary<string, object?> row, string name)
    {
        if (!row.TryGetValue(name, out var v) || v is null) return null;
        try { return Convert.ToDecimal(v); } catch { return null; }
    }
}
