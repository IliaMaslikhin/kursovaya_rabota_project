using System;
using OilErp.Core.Dto;
using OilErp.Core.Contracts;
using OilErp.Infrastructure.Adapters;
using OilErp.Infrastructure.Config;

namespace OilErp.Ui.Services;

/// <summary>
/// Создаёт IStoragePort для central и заводских профилей по строкам окружения.
/// </summary>
public sealed class StoragePortFactory
{
    private readonly IStoragePort central;

    public StoragePortFactory(IStoragePort central)
    {
        this.central = central ?? throw new ArgumentNullException(nameof(central));
    }

    public IStoragePort Central => central;

    public IStoragePort ForPlant(string plant)
    {
        var profile = DetectProfile(plant);
        var cfg = StorageConfigProvider.GetConfig(profile);
        if (string.IsNullOrWhiteSpace(cfg.ConnectionString))
        {
            // Фоллбек на центральное подключение, чтобы не ронять UI без заводской строки.
            return central;
        }

        return new StorageAdapter(cfg);
    }

    private static DatabaseProfile DetectProfile(string plant)
    {
        if (string.Equals(plant, "KRNPZ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(plant, "KNPZ", StringComparison.OrdinalIgnoreCase))
            return DatabaseProfile.PlantKrnpz;
        return DatabaseProfile.PlantAnpz;
    }
}
