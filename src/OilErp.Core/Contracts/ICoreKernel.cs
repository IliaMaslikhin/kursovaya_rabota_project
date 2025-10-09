namespace OilErp.Core.Contracts;

/// <summary>
/// Core kernel interface providing access to storage operations
/// </summary>
public interface ICoreKernel
{
    /// <summary>
    /// Gets the storage port for database operations
    /// </summary>
    IStoragePort Storage { get; }
}
