using System;
using System.Collections.Generic;
using System.Threading;
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

    private KernelGateway(IStoragePort storage, bool isLive, string statusMessage, BootstrapResult? bootstrapInfo, StorageConfig? storageConfig)
    {
        Storage = storage;
        StorageFactory = new StoragePortFactory(storage);
        IsLive = isLive;
        StatusMessage = statusMessage;
        BootstrapInfo = bootstrapInfo;
        StorageConfig = storageConfig;
    }

    public IStoragePort Storage { get; }
    public StoragePortFactory StorageFactory { get; }

    public bool IsLive { get; }

    public string StatusMessage { get; }

    public BootstrapResult? BootstrapInfo { get; }

    public StorageConfig? StorageConfig { get; }

    public static KernelGateway Create(string connectionString)
    {
        var config = new StorageConfig(connectionString ?? throw new ArgumentNullException(nameof(connectionString)), 30, ResolveDisableRoutineCacheFlag());
        AppLogger.Info($"[ui] init kernel gateway with conn='{config.ConnectionString}'");
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
        ValidateConnection(storage);
        var okStatus = BuildSuccessStatus(bootstrap);
        AppLogger.Info($"[ui] storage ready: {okStatus}");
        return new KernelGateway(storage, true, okStatus, bootstrap, config);
    }

    private static string BuildSuccessStatus(BootstrapResult bootstrap)
    {
        var firstRun = bootstrap.IsFirstRun ? " · первичный запуск" : string.Empty;
        var guideHint = string.IsNullOrWhiteSpace(bootstrap.GuidePath) ? string.Empty : $" · гайд: {bootstrap.GuidePath}";
        return $"Подключено к StorageAdapter (профиль {bootstrap.Profile}, код {bootstrap.MachineCode}{firstRun}){guideHint}";
    }

    private static string BuildFailureStatus(BootstrapResult bootstrap)
    {
        var codeHint = string.IsNullOrWhiteSpace(bootstrap.MachineCode) ? string.Empty : $" · код {bootstrap.MachineCode}";
        var guideHint = string.IsNullOrWhiteSpace(bootstrap.GuidePath) ? string.Empty : $" · гайд: {bootstrap.GuidePath}";
        return $"БД не готова: {bootstrap.ErrorMessage ?? "не удалось инициализировать БД"}{codeHint}{guideHint}";
    }

    private static void ValidateConnection(IStoragePort storage)
    {
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
}
