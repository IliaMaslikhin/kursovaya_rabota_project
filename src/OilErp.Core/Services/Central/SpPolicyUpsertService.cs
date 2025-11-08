using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Обертка над public.sp_policy_upsert. См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
public class SpPolicyUpsertService : AppServiceBase
{
    public SpPolicyUpsertService(IStoragePort storage) : base(storage) { }

    public async Task<int> sp_policy_upsertAsync(
        string p_name,
        decimal p_low,
        decimal p_med,
        decimal p_high,
        CancellationToken ct = default)
    {
        var spec = new CommandSpec(
            OperationNames.Central.SpPolicyUpsert,
            new Dictionary<string, object?>
            {
                ["p_name"] = p_name,
                ["p_low"] = p_low,
                ["p_med"] = p_med,
                ["p_high"] = p_high,
            }
        );
        return await Storage.ExecuteCommandAsync(spec, ct);
    }
}

