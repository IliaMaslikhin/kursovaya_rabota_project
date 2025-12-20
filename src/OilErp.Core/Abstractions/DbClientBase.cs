using OilErp.Core.Contracts;
using OilErp.Core.Dto;

namespace OilErp.Core.Abstractions;

/// <summary>
/// Базовый класс для клиентов базы: тут общая обвязка, конкретика в наследниках.
/// </summary>
public abstract class DbClientBase : IStoragePort
{
    /// <inheritdoc />
    public abstract Task<IReadOnlyList<T>> ExecuteQueryAsync<T>(QuerySpec spec, CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<int> ExecuteCommandAsync(CommandSpec spec, CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<IStorageTransaction> BeginTransactionAsync(CancellationToken ct = default);
}
