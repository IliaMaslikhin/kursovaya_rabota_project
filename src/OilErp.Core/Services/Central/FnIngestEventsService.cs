using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Обертка над public.fn_ingest_events. См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
public class FnIngestEventsService : AppServiceBase
{
    public FnIngestEventsService(IStoragePort storage) : base(storage) { }

    public async Task<int> fn_ingest_eventsAsync(
        int p_limit,
        CancellationToken ct = default)
    {
        var spec = new CommandSpec(
            OperationNames.Central.EventsIngest,
            new Dictionary<string, object?>
            {
                ["p_limit"] = p_limit,
            }
        );
        return await Storage.ExecuteCommandAsync(spec, ct);
    }
}

