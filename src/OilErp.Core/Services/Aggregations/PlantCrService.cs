using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Services.Central;

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
        decimal? mean = row?.CrMean;
        decimal? p90 = row?.CrP90;
        int assets = row?.AssetsCount ?? 0;
        return new PlantCrAggregateDto(plant, from, to, mean, p90, assets);
    }
}
