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
        // 1) Получаем детальные строки CR из central.fn_top_assets_by_cr с большим лимитом
        var topSvc = new FnTopAssetsByCrService(Storage);
        var rows = await topSvc.fn_top_assets_by_crAsync(100000, ct);

        // 2) Фильтруем по дате (updated_at в окне [from, to]) и по заводу (через fn_asset_summary_json)
        var summarySvc = new FnAssetSummaryJsonService(Storage);
        var crList = new List<decimal>();
        var plantCache = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var r in rows)
        {
            if (!r.TryGetValue("updated_at", out var upd) || upd is not DateTime dt) continue;
            if (dt < from || dt > to) continue;
            if (!r.TryGetValue("asset_code", out var ac) || ac is not string assetCode || string.IsNullOrWhiteSpace(assetCode)) continue;

            if (!plantCache.TryGetValue(assetCode, out var assetPlant))
            {
                var dto = await summarySvc.fn_asset_summary_jsonAsync(assetCode, null, ct);
                assetPlant = dto?.Asset.PlantCode;
                plantCache[assetCode] = assetPlant;
            }
            if (!string.Equals(assetPlant, plant, StringComparison.OrdinalIgnoreCase)) continue;

            if (r.TryGetValue("cr", out var crVal) && crVal is not null)
            {
                try
                {
                    var cr = Convert.ToDecimal(crVal);
                    crList.Add(cr);
                }
                catch { /* skip non-convertible */ }
            }
        }

        // 3) Агрегация: среднее и P90 (если список пуст — null)
        decimal? mean = null, p90 = null;
        if (crList.Count > 0)
        {
            mean = crList.Average();
            var sorted = crList.OrderBy(x => x).ToArray();
            var idx = (int)Math.Ceiling(0.9m * sorted.Length) - 1;
            idx = Math.Clamp(idx, 0, sorted.Length - 1);
            p90 = sorted[idx];
        }

        return new PlantCrAggregateDto(plant, from, to, mean, p90, crList.Count);
    }
}

