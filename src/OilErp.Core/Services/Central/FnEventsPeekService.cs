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

    public async Task<IReadOnlyList<Dictionary<string, object?>>> fn_events_peekAsync(
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
        return await Storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec, ct);
    }
}

