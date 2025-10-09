using OilErp.Core.Abstractions;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;

namespace OilErp.Infrastructure.Adapters;

/// <summary>
/// Storage adapter implementation that throws NotImplementedException for all operations
/// </summary>
public class StorageAdapter : DbClientBase
{
    /// <inheritdoc />
    public override Task<IReadOnlyList<T>> ExecuteQueryAsync<T>(QuerySpec spec, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task<int> ExecuteCommandAsync(CommandSpec spec, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override event EventHandler<DbNotification>? Notified
    {
        add { }
        remove { }
    }
}
