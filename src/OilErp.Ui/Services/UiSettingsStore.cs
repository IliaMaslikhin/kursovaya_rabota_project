using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OilErp.Ui.Services;

/// <summary>
/// Очень простой стор для настроек UI (в файл в папке ApplicationData).
/// </summary>
public static class UiSettingsStore
{
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    public static UiSettings Load()
    {
        lock (Sync)
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return UiSettings.CreateDefault();
                }

                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<UiSettings>(json, JsonOptions);
                return settings ?? UiSettings.CreateDefault();
            }
            catch
            {
                return UiSettings.CreateDefault();
            }
        }
    }

    public static void Save(UiSettings settings)
    {
        if (settings is null) return;
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Настройки не должны ломать запуск UI.
            }
        }
    }

    private static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OilErp");

    private static string SettingsPath => Path.Combine(SettingsDirectory, "ui-settings.json");
}

/// <summary>
/// Настройки UI. Пока тут только выбор политики риска на завод.
/// </summary>
public sealed record UiSettings(
    Dictionary<string, string> PlantRiskPolicies,
    string? LastPolicyForAll)
{
    public static UiSettings CreateDefault() =>
        new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), "default");
}

