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
        var conn = ReadConnection(profile);
        if (string.IsNullOrWhiteSpace(conn))
        {
            // Фоллбек на центральное подключение, чтобы не ронять UI без заводской строки.
            return central;
        }

        var timeout = ReadTimeout();
        return new StorageAdapter(new StorageConfig(conn!, timeout));
    }

    private static DatabaseProfile DetectProfile(string plant)
    {
        if (string.Equals(plant, "KRNPZ", StringComparison.OrdinalIgnoreCase))
            return DatabaseProfile.PlantKrnpz;
        return DatabaseProfile.PlantAnpz;
    }

    private static string? ReadConnection(DatabaseProfile profile)
    {
        var suffix = profile switch
        {
            DatabaseProfile.PlantKrnpz => "_KRNPZ",
            DatabaseProfile.PlantAnpz => "_ANPZ",
            _ => string.Empty
        };

        string[] keys =
        {
            $"OILERP__DB__CONN{suffix}",
            $"OIL_ERP_PG{suffix}",
            "OILERP__DB__CONN",
            "OIL_ERP_PG"
        };

        foreach (var key in keys)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    private static int ReadTimeout()
    {
        var timeoutVar = Environment.GetEnvironmentVariable("OILERP__DB__TIMEOUT_SEC")
                         ?? Environment.GetEnvironmentVariable("OIL_ERP_PG_TIMEOUT");
        return int.TryParse(timeoutVar, out var t) ? t : 30;
    }
}
