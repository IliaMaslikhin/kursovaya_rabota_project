using OilErp.Core.Dto;

namespace OilErp.Core.Contracts;

/// <summary>
/// Интерфейс для работы с базой: можно выполнять запросы, команды и открывать транзакции.
/// </summary>
public interface IStoragePort
{
    /// <summary>
    /// Выполняет запрос и возвращает список результатов указанного типа.
    /// </summary>
    /// <typeparam name="T">Тип элементов в выдаче</typeparam>
    /// <param name="spec">Описание запроса</param>
    /// <param name="ct">Токен отмены</param>
    Task<IReadOnlyList<T>> ExecuteQueryAsync<T>(QuerySpec spec, CancellationToken ct = default);

    /// <summary>
    /// Выполняет команду (INSERT/UPDATE/DELETE) и возвращает количество затронутых строк.
    /// </summary>
    /// <param name="spec">Описание команды</param>
    /// <param name="ct">Токен отмены</param>
    Task<int> ExecuteCommandAsync(CommandSpec spec, CancellationToken ct = default);

    /// <summary>
    /// Открывает транзакцию и отдаёт обёртку с Commit/Rollback.
    /// </summary>
    /// <param name="ct">Токен отмены</param>
    Task<IStorageTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

/// <summary>
/// Открытая транзакция с явным коммитом или откатом.
/// </summary>
public interface IStorageTransaction : IAsyncDisposable
{
    /// <summary>
    /// Фиксирует все изменения в транзакции.
    /// </summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>
    /// Откатывает транзакцию целиком.
    /// </summary>
    Task RollbackAsync(CancellationToken ct = default);
}
