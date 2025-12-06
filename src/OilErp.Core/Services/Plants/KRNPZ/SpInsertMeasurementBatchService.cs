using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Plants.KRNPZ;

/// <summary>
/// Обертка над public.sp_insert_measurement_batch (KRNPZ). См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
public class SpInsertMeasurementBatchService : AppServiceBase
{
    public SpInsertMeasurementBatchService(IStoragePort storage) : base(storage) { }

    public async Task<int> sp_insert_measurement_batchAsync(
        string p_asset_code,
        string p_points,
        string p_source_plant,
        CancellationToken ct = default)
    {
        p_asset_code = NormalizeCode(p_asset_code);
        p_source_plant = NormalizePlant(p_source_plant) ?? "KRNPZ";

        var spec = new CommandSpec(
            OperationNames.Plant.MeasurementsInsertBatch,
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
