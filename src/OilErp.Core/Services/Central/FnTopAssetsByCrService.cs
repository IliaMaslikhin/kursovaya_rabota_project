using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Обертка над public.fn_top_assets_by_cr. См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
public class FnTopAssetsByCrService : AppServiceBase
{
    public FnTopAssetsByCrService(IStoragePort storage) : base(storage) { }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> fn_top_assets_by_crAsync(
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
        return await Storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec, ct);
    }
}

