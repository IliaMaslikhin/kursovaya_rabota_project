using OilErp.Core.Contracts;

namespace OilErp.Infrastructure.Adapters;

/// <summary>
/// Простой адаптер ядра: хранит ссылку на хранилище.
/// </summary>
public class KernelAdapter : ICoreKernel
{
    private readonly IStoragePort _storage;

    /// <summary>
    /// Создаёт адаптер и проверяет, что хранилище передано.
    /// </summary>
    /// <param name="storage">Реализация доступа к базе</param>
    public KernelAdapter(IStoragePort storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <inheritdoc />
    public IStoragePort Storage => _storage;
}
