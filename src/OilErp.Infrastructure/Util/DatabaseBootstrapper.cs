using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Npgsql;
using OilErp.Core.Dto;

namespace OilErp.Bootstrap;

public sealed record BootstrapResult(
    bool Success,
    DatabaseProfile Profile,
    bool IsFirstRun,
    string MachineCode,
    string? ErrorMessage,
    string? GuidePath)
{
    public static BootstrapResult Ok(DatabaseProfile profile, bool isFirstRun, string machineCode) =>
        new(true, profile, isFirstRun, machineCode, null, null);

    public static BootstrapResult Fail(DatabaseProfile profile, bool isFirstRun, string machineCode, string error, string? guidePath) =>
        new(false, profile, isFirstRun, machineCode, error, guidePath);
}

/// <summary>
/// Общий бутстраппер для UI/Tests/Infra.
/// </summary>
public sealed class DatabaseBootstrapper
{
    private readonly string connectionString;
    private readonly string? guideSourcePath;

    public DatabaseBootstrapper(string connectionString)
    {
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        guideSourcePath = LocateGuideFile();
    }

    public async Task<BootstrapResult> EnsureProvisionedAsync()
    {
        var isFirstRun = FirstRunTracker.IsFirstRun(out var machineCode);
        AppLogger.Info($"[bootstrap] старт проверки профиля (firstRun={isFirstRun}, machine={machineCode})");
        var centralInspector = new DatabaseInventoryInspector(WithDatabase(connectionString, "central"));
        try
        {
            await EnsureDatabasesAsync();

            var inspectors = new[]
            {
                centralInspector,
                new DatabaseInventoryInspector(WithDatabase(connectionString, "anpz")),
                new DatabaseInventoryInspector(WithDatabase(connectionString, "krnpz"))
            };

            foreach (var inspector in inspectors)
            {
                var verification = await inspector.VerifyAsync();
                inspector.PrintSummary();

                if (verification.Success)
                    continue;

                var error = $"Профиль {inspector.Profile}: {verification.ErrorMessage ?? "Database verification failed"}";
                var guidePath = TryCopyGuideToDesktop(inspector.Profile, error, machineCode);
                AppLogger.Error($"[bootstrap] проверка не прошла: {error}");
                return BootstrapResult.Fail(inspector.Profile, isFirstRun, machineCode, error, guidePath);
            }

            FirstRunTracker.MarkCompleted(machineCode);
            AppLogger.Info($"[bootstrap] проверка успешна profile={centralInspector.Profile}");
            return BootstrapResult.Ok(centralInspector.Profile, isFirstRun, machineCode);
        }
        catch (Exception ex)
        {
            var guidePath = TryCopyGuideToDesktop(centralInspector.Profile, ex.Message, machineCode);
            AppLogger.Error($"[bootstrap] ошибка: {ex.Message}");
            return BootstrapResult.Fail(centralInspector.Profile, isFirstRun, machineCode, ex.Message, guidePath);
        }
        finally
        {
            AppLogger.Info("[bootstrap] EnsureProvisioned finished");
        }
    }

    private async Task EnsureDatabasesAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var targetDb = string.IsNullOrWhiteSpace(builder.Database) ? "central" : builder.Database;
        var dbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "central", "anpz", "krnpz", targetDb };

        builder.Database = "postgres";
        var adminConnString = builder.ConnectionString;

        await using var conn = new NpgsqlConnection(adminConnString);
        await conn.OpenAsync();
        foreach (var db in dbs)
        {
            await using var existsCmd = conn.CreateCommand();
            existsCmd.CommandText = "select 1 from pg_database where datname=@name";
            existsCmd.Parameters.AddWithValue("name", db);
            var exists = await existsCmd.ExecuteScalarAsync();
            if (exists != null && exists != DBNull.Value)
            {
                continue;
            }

            AppLogger.Info($"[bootstrap] создаём базу '{db}'");
            await using var createCmd = conn.CreateCommand();
            createCmd.CommandText = $"create database \"{db}\"";
            await createCmd.ExecuteNonQueryAsync();
        }
        AppLogger.Info("[bootstrap] ensure databases completed");
    }

    private static string WithDatabase(string baseConnectionString, string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = database
        };
        return builder.ConnectionString;
    }

    private string? TryCopyGuideToDesktop(DatabaseProfile profile, string? error, string machineCode)
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(desktop) || !Directory.Exists(desktop))
            {
                return null;
            }

            var lines = new List<string>
            {
                "# OilErp Database Setup",
                $"Profile: {profile}",
                $"Machine code: {machineCode}",
                $"Detected at: {DateTime.UtcNow:O}",
                string.Empty,
                "Проблема при создании/проверке базы данных.",
                $"Ошибка: {error ?? "нет деталей"}",
                string.Empty,
                "Шаги из руководства:"
            };

            if (guideSourcePath != null)
            {
                lines.AddRange(File.ReadAllLines(guideSourcePath));
            }
            else
            {
                lines.Add("Не найден файл руководства docs/README.md рядом с проектом.");
            }

            var target = Path.Combine(desktop, "OilErp_Database_Guide.md");
            File.WriteAllLines(target, lines);
            return target;
        }
        catch
        {
            return null;
        }
    }

    private static string? LocateGuideFile()
    {
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "docs", "README.md");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }
}
