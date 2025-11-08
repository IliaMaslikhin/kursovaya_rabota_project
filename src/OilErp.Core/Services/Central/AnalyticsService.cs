using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Сервис-обертка для центральных аналитических операций.
/// См. карту соответствий: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
public class AnalyticsService : AppServiceBase
{
    public AnalyticsService(IStoragePort storage) : base(storage) { }

    /// <summary>
    /// Обертка над SQL-операцией public.fn_asset_summary_json.
    /// Возвращает JSON (SELECT schema.fn(... )::jsonb → здесь как строка).
    /// См. MAPPING: src/OilErp.Infrastructure/Readme.Mapping.md (Central.AnalyticsAssetSummary → public.fn_asset_summary_json)
    /// </summary>
    /// <param name="p_asset_code">Код актива (обязательный)</param>
    /// <param name="p_policy_name">Имя политики риска (по умолчанию 'default')</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>JSON как строка, либо null если пусто</returns>
    public async Task<string?> GetAssetSummaryJsonAsync(string p_asset_code, string? p_policy_name = "default", CancellationToken ct = default)
    {
        var spec = new QuerySpec(
            OperationNames.Central.AnalyticsAssetSummary,
            new Dictionary<string, object?>
            {
                ["p_asset_code"] = p_asset_code,
                ["p_policy_name"] = p_policy_name
            },
            TimeoutSeconds: null // таймаут возьмется из конфигурации адаптера
        );

        var rows = await Storage.ExecuteQueryAsync<string>(spec, ct);
        return rows.FirstOrDefault();
    }
}

