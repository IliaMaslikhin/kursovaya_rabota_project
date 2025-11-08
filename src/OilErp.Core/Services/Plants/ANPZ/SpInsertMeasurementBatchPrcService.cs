using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Plants.ANPZ;

/// <summary>
/// Обертка над public.sp_insert_measurement_batch_prc (ANPZ). См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
public class SpInsertMeasurementBatchPrcService : AppServiceBase
{
    public SpInsertMeasurementBatchPrcService(IStoragePort storage) : base(storage) { }

    public async Task<int> sp_insert_measurement_batch_prcAsync(
        string p_asset_code,
        string p_points,
        string p_source_plant,
        CancellationToken ct = default)
    {
        var spec = new CommandSpec(
            OperationNames.Plant.MeasurementsInsertBatchPrc,
            new Dictionary<string, object?>
            {
                ["p_asset_code"] = p_asset_code,
                ["p_points"] = p_points,
                ["p_source_plant"] = p_source_plant,
            }
        );
        return await Storage.ExecuteCommandAsync(spec, ct);
    }
}

