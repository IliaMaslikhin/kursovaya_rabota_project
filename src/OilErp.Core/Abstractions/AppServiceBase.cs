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
