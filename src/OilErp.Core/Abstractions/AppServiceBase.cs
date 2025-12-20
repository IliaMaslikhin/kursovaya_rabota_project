using OilErp.Core.Contracts;
using OilErp.Core.Dto;

namespace OilErp.Core.Abstractions;

/// <summary>
/// Базовый класс для сервисов: держит ссылку на хранилище и общую нормализацию ввода.
/// </summary>
public abstract class AppServiceBase
{
    /// <summary>
    /// Хранилище, через которое сервис ходит в базу.
    /// </summary>
    protected readonly IStoragePort Storage;

    /// <summary>
    /// Создаёт сервис и проверяет, что хранилище передано.
    /// </summary>
    /// <param name="storage">Конкретная реализация хранилища</param>
    protected AppServiceBase(IStoragePort storage)
    {
        Storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    protected static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    protected static string NormalizeCode(string value)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null) throw new ArgumentNullException(nameof(value));
        return normalized;
    }

    protected static string? NormalizePlant(string? value)
    {
        var normalized = NormalizeOptional(value);
        return normalized?.ToUpperInvariant();
    }
}
