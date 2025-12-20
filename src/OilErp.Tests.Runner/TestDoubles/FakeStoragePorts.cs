using OilErp.Core.Contracts;
using OilErp.Core.Dto;

namespace OilErp.Tests.Runner.TestDoubles;

/// <summary>Простая фейковая реализация IStoragePort для тестов в памяти.</summary>
public class FakeStoragePort : IStoragePort
{
    private readonly List<QuerySpec> _queryHistory = new();
    private readonly List<CommandSpec> _commandHistory = new();
    private readonly Dictionary<string, int> _methodCallCounts = new();
    private readonly List<FakeTransaction> _transactions = new();
    private int _artificialDelayMs;

    public IReadOnlyList<QuerySpec> QueryHistory => _queryHistory.AsReadOnly();
    public IReadOnlyList<CommandSpec> CommandHistory => _commandHistory.AsReadOnly();
    public IReadOnlyDictionary<string, int> MethodCallCounts => _methodCallCounts.AsReadOnly();
    public IReadOnlyList<FakeTransaction> Transactions => _transactions.AsReadOnly();

    public int ArtificialDelayMs
    {
        get => _artificialDelayMs;
        set => _artificialDelayMs = Math.Max(0, value);
    }

    public async Task<IReadOnlyList<T>> ExecuteQueryAsync<T>(QuerySpec spec, CancellationToken ct = default)
    {
        IncrementCallCount(nameof(ExecuteQueryAsync));
        _queryHistory.Add(spec);

        if (_artificialDelayMs > 0)
        {
            await Task.Delay(_artificialDelayMs, ct);
        }

        ct.ThrowIfCancellationRequested();
        return new List<T>();
    }

    public async Task<int> ExecuteCommandAsync(CommandSpec spec, CancellationToken ct = default)
    {
        IncrementCallCount(nameof(ExecuteCommandAsync));
        _commandHistory.Add(spec);

        if (_artificialDelayMs > 0)
        {
            await Task.Delay(_artificialDelayMs, ct);
        }

        ct.ThrowIfCancellationRequested();
        return 1;
    }

    public Task<IStorageTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        IncrementCallCount(nameof(BeginTransactionAsync));
        var transaction = new FakeTransaction();
        _transactions.Add(transaction);
        return Task.FromResult<IStorageTransaction>(transaction);
    }

    public void Clear()
    {
        _queryHistory.Clear();
        _commandHistory.Clear();
        _methodCallCounts.Clear();
        _transactions.Clear();
    }

    private void IncrementCallCount(string methodName)
    {
        if (_methodCallCounts.ContainsKey(methodName))
        {
            _methodCallCounts[methodName]++;
        }
        else
        {
            _methodCallCounts[methodName] = 1;
        }
    }
}

/// <summary>Фейковая транзакция: помечает коммит/откат и сообщает, что её закрыли.</summary>
public class FakeTransaction : IStorageTransaction
{
    public bool IsCommitted { get; private set; }
    public bool IsRolledBack { get; private set; }
    public bool IsDisposed { get; private set; }

    public Task CommitAsync(CancellationToken ct = default)
    {
        if (IsDisposed) throw new InvalidOperationException("Transaction is already disposed");
        if (IsRolledBack) throw new InvalidOperationException("Cannot commit after rollback");
        IsCommitted = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken ct = default)
    {
        if (IsDisposed) throw new InvalidOperationException("Transaction is already disposed");
        IsRolledBack = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>Фейковое хранилище-обёртка над реальным IStoragePort для ручного контроля.</summary>
public sealed class TransactionalFakeStoragePort : IStoragePort
{
    private readonly IStoragePort inner;
    private readonly List<QuerySpec> queries = new();
    private readonly List<CommandSpec> commands = new();

    public TransactionalFakeStoragePort(IStoragePort inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public IReadOnlyList<QuerySpec> Queries => queries;
    public IReadOnlyList<CommandSpec> Commands => commands;

    public Task<IReadOnlyList<T>> ExecuteQueryAsync<T>(QuerySpec spec, CancellationToken ct = default)
    {
        queries.Add(spec);
        return inner.ExecuteQueryAsync<T>(spec, ct);
    }

    public Task<int> ExecuteCommandAsync(CommandSpec spec, CancellationToken ct = default)
    {
        commands.Add(spec);
        return inner.ExecuteCommandAsync(spec, ct);
    }

    public Task<IStorageTransaction> BeginTransactionAsync(CancellationToken ct = default) => inner.BeginTransactionAsync(ct);
}
