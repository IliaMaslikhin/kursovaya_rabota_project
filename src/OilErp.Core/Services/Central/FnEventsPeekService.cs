using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Обертка над public.fn_events_peek. См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
public class FnEventsPeekService : AppServiceBase
{
    public FnEventsPeekService(IStoragePort storage) : base(storage) { }

    public async Task<IReadOnlyList<EventPeekItemDto>> fn_events_peekAsync(
        int p_limit,
        CancellationToken ct = default)
    {
        var spec = new QuerySpec(
            OperationNames.Central.EventsPeek,
            new Dictionary<string, object?>
            {
                ["p_limit"] = p_limit,
            }
        );
        var rows = await Storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec, ct);
        return rows.Select(Map).ToList();
    }

    private static EventPeekItemDto Map(Dictionary<string, object?> row)
    {
        return new EventPeekItemDto(
            Id: Convert.ToInt64(row["id"]),
            EventType: row.TryGetValue("event_type", out var t) ? t?.ToString() : null,
            SourcePlant: row.TryGetValue("source_plant", out var sp) ? sp?.ToString() : null,
            PayloadJson: row.TryGetValue("payload_json", out var p) ? p?.ToString() ?? "{}" : "{}",
            CreatedAt: row.TryGetValue("created_at", out var c) && DateTime.TryParse(c?.ToString(), out var dt) ? dt : DateTime.MinValue);
    }
}
