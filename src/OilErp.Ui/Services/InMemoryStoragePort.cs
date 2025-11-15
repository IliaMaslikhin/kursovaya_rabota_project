using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Ui.Services;

/// <summary>
/// Простая in-memory реализация хранилища для офлайн-режима UI.
/// </summary>
public sealed class InMemoryStoragePort : IStoragePort
{
    private readonly List<string> _commandLog = new();
    private EventHandler<DbNotification>? notified;

    public event EventHandler<DbNotification>? Notified
    {
        add => notified += value;
        remove => notified -= value;
    }

    public Task<IReadOnlyList<T>> ExecuteQueryAsync<T>(QuerySpec spec, CancellationToken ct = default)
    {
        if (spec.OperationName == OperationNames.Central.CalcCr && typeof(T) == typeof(decimal))
        {
            var prevThk = Convert.ToDecimal(spec.Parameters.GetValueOrDefault("prev_thk", 10m));
            var prevDate = spec.Parameters.TryGetValue("prev_date", out var prevDt)
                ? Convert.ToDateTime(prevDt, CultureInfo.InvariantCulture)
                : DateTime.UtcNow.AddDays(-10);
            var lastThk = Convert.ToDecimal(spec.Parameters.GetValueOrDefault("last_thk", 9.5m));
            var lastDate = spec.Parameters.TryGetValue("last_date", out var lastDt)
                ? Convert.ToDateTime(lastDt, CultureInfo.InvariantCulture)
                : DateTime.UtcNow;
            var span = Math.Max(1.0, (lastDate - prevDate).TotalDays);
            var cr = (prevThk - lastThk) / (decimal)span;
            var list = new List<T>(1) { (T)(object)cr };
            return Task.FromResult<IReadOnlyList<T>>(list);
        }

        return Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());
    }

    public Task<int> ExecuteCommandAsync(CommandSpec spec, CancellationToken ct = default)
    {
        _commandLog.Add($"{DateTime.UtcNow:O} · {spec.OperationName}");
        notified?.Invoke(this, new DbNotification("in-memory", spec.OperationName, Environment.ProcessId));
        return Task.FromResult(1);
    }

    public Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IAsyncDisposable>(new AsyncDisposableAction(() => Task.CompletedTask));
    }

    private sealed class AsyncDisposableAction : IAsyncDisposable
    {
        private readonly Func<Task> _dispose;

        public AsyncDisposableAction(Func<Task> dispose)
        {
            _dispose = dispose;
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask(_dispose());
        }
    }
}
