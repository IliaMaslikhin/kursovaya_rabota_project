using System;
using System.IO;

namespace OilErp.Bootstrap;

/// <summary>
/// Минимальный логгер для UI (консоль + файл в %APPDATA%/OilErp/logs).
/// </summary>
internal static class AppLogger
{
    private static readonly object Sync = new();
    private static readonly string LogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OilErp", "logs");
    private static readonly string SessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
    private static readonly string LogPath = Path.Combine(LogDirectory, $"app-{SessionId}.log");
    private static bool _initialized;

    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        EnsureInitialized();
        var line = $"{DateTime.UtcNow:O} [{level}] {message}";
        Console.WriteLine(line);
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // ignore logging errors
        }
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (Sync)
        {
            if (_initialized) return;
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(LogPath, $"=== Начало сессии {SessionId} UTC ==={Environment.NewLine}");
            _initialized = true;
        }
    }
}
