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

    public async Task<IReadOnlyList<Dictionary<string, object?>>> fn_plant_cr_statsAsync(
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
        return await Storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec, ct);
    }
}
