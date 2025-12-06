using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;
using OilErp.Core.Services.Dtos;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Обертка над public.fn_top_assets_by_cr. См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
public class FnTopAssetsByCrService : AppServiceBase
{
    public FnTopAssetsByCrService(IStoragePort storage) : base(storage) { }

    public async Task<IReadOnlyList<TopAssetCrDto>> fn_top_assets_by_crAsync(
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
        var rows = await Storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec, ct);
        return rows
            .Select(static r => new TopAssetCrDto
            {
                AssetCode = ReadString(r, "asset_code", "asset"),
                Cr = TryDecimal(r, "cr"),
                UpdatedAt = TryDateTime(r, "updated_at")
            })
            .Where(x => x.AssetCode is not null)
            .ToList();
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> row, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (row.TryGetValue(k, out var v) && v is not null) return v.ToString();
            var kvp = row.FirstOrDefault(x => string.Equals(x.Key, k, StringComparison.OrdinalIgnoreCase));
            if (kvp.Value is not null) return kvp.Value.ToString();
        }
        return null;
    }

    private static decimal? TryDecimal(IReadOnlyDictionary<string, object?> row, params string[] keys)
    {
        var s = ReadString(row, keys);
        return decimal.TryParse(s, out var d) ? d : null;
    }

    private static DateTime? TryDateTime(IReadOnlyDictionary<string, object?> row, params string[] keys)
    {
        var s = ReadString(row, keys);
        return DateTime.TryParse(s, out var dt) ? dt : null;
    }
}
