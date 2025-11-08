using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Обертка над public.fn_calc_cr. См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
public class FnCalcCrService : AppServiceBase
{
    public FnCalcCrService(IStoragePort storage) : base(storage) { }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> fn_calc_crAsync(
        decimal prev_thk,
        DateTime prev_date,
        decimal last_thk,
        DateTime last_date,
        CancellationToken ct = default)
    {
        var spec = new QuerySpec(
            OperationNames.Central.CalcCr,
            new Dictionary<string, object?>
            {
                ["prev_thk"] = prev_thk,
                ["prev_date"] = prev_date,
                ["last_thk"] = last_thk,
                ["last_date"] = last_date,
            }
        );
        var rows = await Storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec, ct);
        return rows;
    }
}

