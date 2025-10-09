using OilErp.Core.Contracts;
using OilErp.Core.Dto;

namespace OilErp.Core.Abstractions;

/// <summary>
/// Base class for application service implementations
/// </summary>
public abstract class AppServiceBase
{
    /// <summary>
    /// Storage port for database operations
    /// </summary>
    protected readonly IStoragePort Storage;

    /// <summary>
    /// Initializes a new instance of the AppServiceBase class
    /// </summary>
    /// <param name="storage">Storage port instance</param>
    protected AppServiceBase(IStoragePort storage)
    {
        Storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }
}
