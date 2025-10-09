using OilErp.Core.Contracts;

namespace OilErp.Infrastructure.Adapters;

/// <summary>
/// Kernel adapter implementation that provides access to storage operations
/// </summary>
public class KernelAdapter : ICoreKernel
{
    private readonly IStoragePort _storage;

    /// <summary>
    /// Initializes a new instance of the KernelAdapter class
    /// </summary>
    /// <param name="storage">Storage port instance</param>
    public KernelAdapter(IStoragePort storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <inheritdoc />
    public IStoragePort Storage => _storage;
}
