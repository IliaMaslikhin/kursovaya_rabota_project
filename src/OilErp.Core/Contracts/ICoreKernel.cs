namespace OilErp.Core.Contracts;

/// <summary>
/// Ядро, которое отдаёт доступ к хранилищу.
/// </summary>
public interface ICoreKernel
{
    /// <summary>
    /// Хранилище, через которое ходим в базу.
    /// </summary>
    IStoragePort Storage { get; }
}
