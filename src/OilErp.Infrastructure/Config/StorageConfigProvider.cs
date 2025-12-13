using OilErp.Core.Dto;

namespace OilErp.Infrastructure.Config;

/// <summary>
/// Унифицированное чтение строк подключения из окружения для central/plant профилей.
/// </summary>
public static class StorageConfigProvider
{
    public static StorageConfig GetConfig(DatabaseProfile profile = DatabaseProfile.Central)
    {
        var conn = ResolveConnection(profile) ?? "Host=localhost;Username=postgres;Password=postgres;Database=postgres";
        var timeout = ResolveTimeout();
        var disableRoutineCache = ResolveDisableRoutineCache();
        return new StorageConfig(conn, timeout, disableRoutineCache);
    }

    private static string? ResolveConnection(DatabaseProfile profile)
    {
        var suffix = profile switch
        {
            DatabaseProfile.PlantAnpz => "_ANPZ",
            DatabaseProfile.PlantKrnpz => "_KRNPZ",
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

    private static int ResolveTimeout()
    {
        var timeoutVar = Environment.GetEnvironmentVariable("OILERP__DB__TIMEOUT_SEC")
                         ?? Environment.GetEnvironmentVariable("OIL_ERP_PG_TIMEOUT");
        return int.TryParse(timeoutVar, out var t) ? t : 30;
    }

    private static bool ResolveDisableRoutineCache()
    {
        var flag = Environment.GetEnvironmentVariable("OILERP__DB__DISABLE_PROC_CACHE");
        if (string.IsNullOrWhiteSpace(flag)) return false;
        return flag.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => false
        };
    }
}
