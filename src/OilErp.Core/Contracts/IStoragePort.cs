using OilErp.Core.Dto;

namespace OilErp.Core.Contracts;

/// <summary>
/// Storage port interface for database operations
/// </summary>
public interface IStoragePort
{
    /// <summary>
    /// Executes a query and returns a collection of results
    /// </summary>
    /// <typeparam name="T">Type of result objects</typeparam>
    /// <param name="spec">Query specification</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of query results</returns>
    Task<IReadOnlyList<T>> ExecuteQueryAsync<T>(QuerySpec spec, CancellationToken ct = default);

    /// <summary>
    /// Executes a command and returns the number of affected rows
    /// </summary>
    /// <param name="spec">Command specification</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of affected rows</returns>
    Task<int> ExecuteCommandAsync(CommandSpec spec, CancellationToken ct = default);

    /// <summary>
    /// Begins a database transaction
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Transaction disposable</returns>
    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Event fired when database notifications are received
    /// </summary>
    event EventHandler<DbNotification>? Notified;
}
