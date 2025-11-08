using System.Text.Json;
using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;
using OilErp.Core.Services.Dtos;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Обертка над public.fn_asset_summary_json. См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
public class FnAssetSummaryJsonService : AppServiceBase
{
    public FnAssetSummaryJsonService(IStoragePort storage) : base(storage) { }

    public async Task<AssetSummaryDto?> fn_asset_summary_jsonAsync(
        string p_asset_code,
        string? p_policy_name,
        CancellationToken ct = default)
    {
        
        var spec = new QuerySpec(
            OperationNames.Central.AnalyticsAssetSummary,
            new Dictionary<string, object?>
            {
                ["p_asset_code"] = p_asset_code,
                ["p_policy_name"] = p_policy_name,
            }
        );
        var json = (await Storage.ExecuteQueryAsync<string>(spec, ct)).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(json)) return null;
        var dto = JsonSerializer.Deserialize<AssetSummaryDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return dto;
    }
}

