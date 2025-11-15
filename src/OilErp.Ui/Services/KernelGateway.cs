using System;
using System.Collections.Generic;
using System.Threading;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;
using OilErp.Infrastructure.Adapters;

namespace OilErp.Ui.Services;

/// <summary>
/// Создаёт подключение к ядру через StorageAdapter либо включает офлайн-режим.
/// </summary>
public sealed class KernelGateway
{
    private const string ConnectionEnvVar = "OIL_ERP_PG";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    private KernelGateway(IStoragePort storage, bool isLive, string statusMessage)
    {
        Storage = storage;
        IsLive = isLive;
        StatusMessage = statusMessage;
    }

    public IStoragePort Storage { get; }

    public bool IsLive { get; }

    public string StatusMessage { get; }

    public static KernelGateway Create()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionEnvVar)))
        {
            return new KernelGateway(
                new InMemoryStoragePort(),
                false,
                $"Переменная {ConnectionEnvVar} не задана — используем офлайн-данные.");
        }

        try
        {
            var storage = new StorageAdapter();
            ValidateConnection(storage);
            return new KernelGateway(storage, true, "Подключено к StorageAdapter (PostgreSQL).");
        }
        catch (Exception ex)
        {
            return new KernelGateway(
                new InMemoryStoragePort(),
                false,
                $"Офлайн-режим (ошибка подключения): {ex.Message}");
        }
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
        storage.ExecuteQueryAsync<decimal>(spec, cts.Token).GetAwaiter().GetResult();
    }
}
