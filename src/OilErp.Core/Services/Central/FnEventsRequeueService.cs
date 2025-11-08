using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Обертка над public.fn_events_requeue. См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
public class FnEventsRequeueService : AppServiceBase
{
    public FnEventsRequeueService(IStoragePort storage) : base(storage) { }

    public async Task<int> fn_events_requeueAsync(
        long[] p_ids,
        CancellationToken ct = default)
    {
        var spec = new CommandSpec(
            OperationNames.Central.EventsRequeue,
            new Dictionary<string, object?>
            {
                ["p_ids"] = p_ids,
            }
        );
        return await Storage.ExecuteCommandAsync(spec, ct);
    }
}

