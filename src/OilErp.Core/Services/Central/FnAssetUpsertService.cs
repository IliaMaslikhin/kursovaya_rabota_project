using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Обертка над public.fn_asset_upsert. См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
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
