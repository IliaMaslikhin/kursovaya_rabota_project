using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Обертка над public.fn_events_enqueue. См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
public class FnEventsEnqueueService : AppServiceBase
{
    public FnEventsEnqueueService(IStoragePort storage) : base(storage) { }

    public async Task<int> fn_events_enqueueAsync(
        string p_event_type,
        string p_source_plant,
        string p_payload,
        CancellationToken ct = default)
    {
        p_event_type = NormalizeOptional(p_event_type) ?? throw new ArgumentNullException(nameof(p_event_type));
        p_source_plant = NormalizePlant(p_source_plant) ?? "ANPZ";
        p_payload = p_payload?.Trim() ?? "{}";

        var spec = new CommandSpec(
            OperationNames.Central.EventsEnqueue,
            new Dictionary<string, object?>
            {
                ["p_event_type"] = p_event_type,
                ["p_source_plant"] = p_source_plant,
                ["p_payload"] = p_payload,
            }
        );
        return await Storage.ExecuteCommandAsync(spec, ct);
    }
}
