using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Обертка над public.sp_asset_upsert. См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
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

