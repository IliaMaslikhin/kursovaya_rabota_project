using System;
using System.IO;

namespace OilErp.Bootstrap;

/// <summary>
/// Общий минимальный логгер (консоль + файл в %APPDATA%/OilErp/logs).
/// </summary>
public static class AppLogger
{
    private static readonly object Sync = new();
    private static readonly string DefaultLogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OilErp", "logs");
    private static readonly string SessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

    private static AppLoggerOptions? _optionsOverride;
    private static readonly Lazy<AppLoggerOptions> EnvOptions = new(() => NormalizeOptions(ReadFromEnvironment()));

    private static bool _initialized;
    private static string _logDirectory = DefaultLogDirectory;
    private static string _logPath = Path.Combine(DefaultLogDirectory, $"app-{SessionId}.log");

    public static void Configure(AppLoggerOptions options)
    {
        lock (Sync)
        {
            _optionsOverride = NormalizeOptions(options);
            _initialized = false;
        }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        var options = CurrentOptions;
        EnsureInitialized(options);
        var line = $"{DateTime.UtcNow:O} [{level}] {message}";

        if (options.LogToConsole)
        {
            Console.WriteLine(line);
        }

        if (!options.LogToFile) return;

        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(_logDirectory);
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Логгер не должен ронять приложение.
        }
    }

    private static void EnsureInitialized(AppLoggerOptions options)
    {
        if (_initialized || !options.LogToFile) { _initialized = _initialized || !options.LogToFile; return; }
        lock (Sync)
        {
            if (_initialized) return;
            Directory.CreateDirectory(_logDirectory);
            File.AppendAllText(_logPath, $"=== Начало сессии {SessionId} UTC ==={Environment.NewLine}");
            _initialized = true;
        }
    }

    private static AppLoggerOptions CurrentOptions
    {
        get
        {
            var options = _optionsOverride ?? EnvOptions.Value;
            _logDirectory = options.LogDirectory ?? DefaultLogDirectory;
            _logPath = Path.Combine(_logDirectory, $"app-{SessionId}.log");
            return options;
        }
    }

    private static AppLoggerOptions ReadFromEnvironment()
    {
        var envConsole = TryParseBool(Environment.GetEnvironmentVariable("OILERP__LOG__TO_CONSOLE"));
        var envFile = TryParseBool(Environment.GetEnvironmentVariable("OILERP__LOG__TO_FILE"));
        var dir = Environment.GetEnvironmentVariable("OILERP__LOG__DIR");
        return new AppLoggerOptions(
            envConsole ?? true,
            envFile ?? true,
            dir);
    }

    private static AppLoggerOptions NormalizeOptions(AppLoggerOptions options)
    {
        var dir = string.IsNullOrWhiteSpace(options.LogDirectory) ? DefaultLogDirectory : options.LogDirectory.Trim();
        return options with { LogDirectory = dir };
    }

    private static bool? TryParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => null
        };
    }
}

public readonly record struct AppLoggerOptions(
    bool LogToConsole = true,
    bool LogToFile = true,
    string? LogDirectory = null);
