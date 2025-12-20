using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OilErp.Core.Dto;
using OilErp.Infrastructure.Adapters;
using OilErp.Infrastructure.Config;
using Npgsql;

namespace OilErp.Tests.Runner;

/// <summary>
/// Утилиты окружения для смоук-тестов: создаёт конфиги и адаптеры.
/// </summary>
public static class TestEnvironment
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<DatabaseProfile, StorageConfig> CachedConfigs = new();

    public static StorageConfig LoadStorageConfig(DatabaseProfile profile = DatabaseProfile.Central)
    {
        lock (SyncRoot)
        {
            if (CachedConfigs.TryGetValue(profile, out var cached)) return cached;
            var resolved = ResolveFromAppSettings(profile) ?? StorageConfigProvider.GetConfig(profile);
            var normalized = NormalizeDatabase(resolved, profile);
            CachedConfigs[profile] = normalized;
            return normalized;
        }
    }

    public static KernelAdapter CreateKernel(DatabaseProfile profile = DatabaseProfile.Central)
    {
        var storage = CreateStorageAdapter(profile);
        return new KernelAdapter(storage);
    }

    public static StorageAdapter CreateStorageAdapter(DatabaseProfile profile = DatabaseProfile.Central) =>
        new StorageAdapter(LoadStorageConfig(profile));

    public static string ConnectionString => LoadStorageConfig().ConnectionString;

    private static StorageConfig? ResolveFromAppSettings(DatabaseProfile profile)
    {
        foreach (var name in new[] { "appsettings.Development.json", "appsettings.json" })
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, name);
                if (!File.Exists(path)) continue;
                using var fs = File.OpenRead(path);
                using var doc = JsonDocument.Parse(fs);
                if (doc.RootElement.TryGetProperty("OILERP", out var oilerp)
                    && oilerp.TryGetProperty("DB", out var db))
                {
                    var suffix = profile switch
                    {
                        DatabaseProfile.PlantAnpz => "_ANPZ",
                        DatabaseProfile.PlantKrnpz => "_KRNPZ",
                        _ => string.Empty
                    };
                    var connProp = string.IsNullOrWhiteSpace(suffix) ? "CONN" : $"CONN{suffix}";
                    var conn = db.TryGetProperty(connProp, out var cEl) ? cEl.GetString() : null;
                    var timeout = db.TryGetProperty("TIMEOUT_SEC", out var tEl) ? tEl.GetInt32() : 30;
                    if (!string.IsNullOrWhiteSpace(conn))
                        return new StorageConfig(conn!, timeout);
                }
            }
            catch
            {
                // игнорируем и переходим к стандартному конфигу
            }
        }

        return null;
    }

    private static StorageConfig NormalizeDatabase(StorageConfig config, DatabaseProfile profile)
    {
        try
        {
            var b = new NpgsqlConnectionStringBuilder(config.ConnectionString);
            var shouldNormalize = string.IsNullOrWhiteSpace(b.Database) || string.Equals(b.Database, "postgres", StringComparison.OrdinalIgnoreCase);
            if (!shouldNormalize) return config;

            b.Database = profile switch
            {
                DatabaseProfile.PlantAnpz => "anpz",
                DatabaseProfile.PlantKrnpz => "krnpz",
                _ => "central"
            };
            return new StorageConfig(b.ConnectionString, config.CommandTimeoutSeconds, config.DisableRoutineMetadataCache);
        }
        catch
        {
            return config;
        }
    }
}
