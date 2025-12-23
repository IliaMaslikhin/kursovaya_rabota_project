using System;
using System.Collections.Generic;
using System.Threading;
using Npgsql;
using OilErp.Bootstrap;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;
using OilErp.Infrastructure.Adapters;
using OilErp.Infrastructure.Config;

#nullable enable

namespace OilErp.Ui.Services;

/// <summary>
/// Создаёт подключение к ядру через StorageAdapter либо включает офлайн-режим.
/// </summary>
public sealed class KernelGateway
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    private KernelGateway(
        IStoragePort storage,
        bool isLive,
        DatabaseProfile targetProfile,
        string statusMessage,
        string actualDatabase,
        BootstrapResult? bootstrapInfo,
        StorageConfig? storageConfig)
    {
        Storage = storage;
        StorageFactory = new StoragePortFactory(storage);
        IsLive = isLive;
        TargetProfile = targetProfile;
        StatusMessage = statusMessage;
        ActualDatabase = actualDatabase;
        BootstrapInfo = bootstrapInfo;
        StorageConfig = storageConfig;
    }

    public IStoragePort Storage { get; }
    public StoragePortFactory StorageFactory { get; }

    public bool IsLive { get; }

    public DatabaseProfile TargetProfile { get; }

    public string StatusMessage { get; }

    public string ActualDatabase { get; }

    public BootstrapResult? BootstrapInfo { get; }

    public StorageConfig? StorageConfig { get; }

    public static KernelGateway Create(string connectionString, DatabaseProfile targetProfile)
    {
        if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));
        var normalizedConn = NormalizeTargetDatabase(connectionString, targetProfile);
        var config = new StorageConfig(normalizedConn, 30, ResolveDisableRoutineCacheFlag());
        AppLogger.Info($"[ui] init kernel gateway with profile={targetProfile} conn='{config.ConnectionString}'");
        BootstrapResult? bootstrap = null;
        var bootstrapper = new DatabaseBootstrapper(config.ConnectionString);
        bootstrap = bootstrapper.EnsureProvisionedAsync().GetAwaiter().GetResult();
        if (!bootstrap.Success)
        {
            var failStatus = BuildFailureStatus(bootstrap);
            AppLogger.Error($"[ui] bootstrap failed: {failStatus}");
            throw new InvalidOperationException(failStatus);
        }

        var storage = new StorageAdapter(config);
        var actualDatabase = ValidateConnection(storage, config.ConnectionString, targetProfile);
        var okStatus = BuildSuccessStatus(bootstrap, actualDatabase);
        AppLogger.Info($"[ui] storage ready: {okStatus}");
        return new KernelGateway(storage, true, targetProfile, okStatus, actualDatabase, bootstrap, config);
    }

    private static string BuildSuccessStatus(BootstrapResult bootstrap, string actualDatabase)
    {
        var dbDisplay = FormatDatabaseDisplayName(actualDatabase);
        var firstRun = bootstrap.IsFirstRun ? " · первичный запуск" : string.Empty;
        var guideHint = string.IsNullOrWhiteSpace(bootstrap.GuidePath) ? string.Empty : $" · гайд: {bootstrap.GuidePath}";
        return $"Подключено ({dbDisplay}) · код {bootstrap.MachineCode}{firstRun}{guideHint}";
    }

    private static string BuildFailureStatus(BootstrapResult bootstrap)
    {
        var codeHint = string.IsNullOrWhiteSpace(bootstrap.MachineCode) ? string.Empty : $" · код {bootstrap.MachineCode}";
        var guideHint = string.IsNullOrWhiteSpace(bootstrap.GuidePath) ? string.Empty : $" · гайд: {bootstrap.GuidePath}";
        return $"БД не готова: {bootstrap.ErrorMessage ?? "не удалось инициализировать БД"}{codeHint}{guideHint}";
    }

    private static string ValidateConnection(IStoragePort storage, string connectionString, DatabaseProfile targetProfile)
    {
        // Базовая проверка подключения
        string actualDatabase;
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "select current_database()";
            actualDatabase = (cmd.ExecuteScalar()?.ToString() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(actualDatabase))
                throw new InvalidOperationException("DB ping returned empty current_database().");
        }

        if (targetProfile != DatabaseProfile.Central)
        {
            AppLogger.Info($"[ui] проверка подключения прошла успешно profile={targetProfile}");
            return actualDatabase;
        }

        var spec = new QuerySpec(
            OperationNames.Central.CalcCr,
            new Dictionary<string, object?>
            {
                ["prev_thk"] = 10.1m,
                ["prev_date"] = DateTime.UtcNow.AddDays(-90),
                ["last_thk"] = 9.9m,
                ["last_date"] = DateTime.UtcNow
            },
            TimeoutSeconds: 5);

        using var cts = new CancellationTokenSource(ProbeTimeout);
        var rows = storage.ExecuteQueryAsync<object>(spec, cts.Token).GetAwaiter().GetResult();
        if (rows.Count == 0)
            throw new InvalidOperationException("fn_calc_cr вернула пустой результат.");
        if (rows[0] is not decimal and not double and not float)
            throw new InvalidOperationException($"fn_calc_cr ожидался decimal, но получено {rows[0].GetType().Name}");
        AppLogger.Info("[ui] проверка fn_calc_cr прошла успешно");
        return actualDatabase;
    }

    private static string NormalizeTargetDatabase(string connectionString, DatabaseProfile profile)
    {
        try
        {
            var b = new NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(b.Database))
            {
                b.Database = profile switch
                {
                    DatabaseProfile.Central => "central",
                    DatabaseProfile.PlantAnpz => "anpz",
                    DatabaseProfile.PlantKrnpz => "krnpz",
                    _ => b.Database
                };
            }
            return b.ConnectionString;
        }
        catch
        {
            return connectionString;
        }
    }

    private static bool ResolveDisableRoutineCacheFlag()
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

    private static string FormatDatabaseDisplayName(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName)) return "?";

        var raw = databaseName.Trim();
        var upper = raw.ToUpperInvariant();
        if (upper.Contains("ANPZ")) return "АНПЗ";
        if (upper.Contains("KRNPZ") || upper.Contains("KNPZ")) return "КНПЗ";
        if (upper.Contains("CENTRAL")) return "Центральная";
        return raw;
    }
}
