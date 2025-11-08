using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Обертка над public.sp_events_enqueue. См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
public class SpEventsEnqueueService : AppServiceBase
{
    public SpEventsEnqueueService(IStoragePort storage) : base(storage) { }

    public async Task<int> sp_events_enqueueAsync(
        string p_event_type,
        string p_source_plant,
        string p_payload,
        CancellationToken ct = default)
    {
        var spec = new CommandSpec(
            OperationNames.Central.SpEventsEnqueue,
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

