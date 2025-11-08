using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Core.Services.Central;

/// <summary>
/// Обертка над public.fn_events_cleanup. См. карту: src/OilErp.Infrastructure/Readme.Mapping.md
/// </summary>
public class FnEventsCleanupService : AppServiceBase
{
    public FnEventsCleanupService(IStoragePort storage) : base(storage) { }

    public async Task<int> fn_events_cleanupAsync(
        TimeSpan p_older_than,
        CancellationToken ct = default)
    {
        var spec = new CommandSpec(
            OperationNames.Central.EventsCleanup,
            new Dictionary<string, object?>
            {
                ["p_older_than"] = p_older_than,
            }
        );
        return await Storage.ExecuteCommandAsync(spec, ct);
    }
}

