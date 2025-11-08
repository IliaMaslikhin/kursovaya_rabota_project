using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Обертка над public.fn_eval_risk. См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
public class FnEvalRiskService : AppServiceBase
{
    public FnEvalRiskService(IStoragePort storage) : base(storage) { }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> fn_eval_riskAsync(
        string p_asset_code,
        string? p_policy_name,
        CancellationToken ct = default)
    {
        var spec = new QuerySpec(
            OperationNames.Central.EvalRisk,
            new Dictionary<string, object?>
            {
                ["p_asset_code"] = p_asset_code,
                ["p_policy_name"] = p_policy_name,
            }
        );
        return await Storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec, ct);
    }
}

