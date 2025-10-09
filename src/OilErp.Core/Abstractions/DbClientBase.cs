using OilErp.Core.Contracts;
using OilErp.Core.Dto;

namespace OilErp.Core.Abstractions;

/// <summary>
/// Base class for database client implementations
/// </summary>
public abstract class DbClientBase : IStoragePort
{
    /// <inheritdoc />
    public abstract Task<IReadOnlyList<T>> ExecuteQueryAsync<T>(QuerySpec spec, CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<int> ExecuteCommandAsync(CommandSpec spec, CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct = default);

    /// <inheritdoc />
    public abstract event EventHandler<DbNotification>? Notified;
}
