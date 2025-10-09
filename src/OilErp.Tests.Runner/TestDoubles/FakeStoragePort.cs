using OilErp.Core.Contracts;
using OilErp.Core.Dto;

namespace OilErp.Tests.Runner.TestDoubles;

/// <summary>
/// In-memory fake implementation of IStoragePort for testing
/// </summary>
public class FakeStoragePort : IStoragePort
{
    private readonly List<QuerySpec> _queryHistory = new();
    private readonly List<CommandSpec> _commandHistory = new();
    private readonly Dictionary<string, int> _methodCallCounts = new();
    private readonly List<DbNotification> _notifications = new();
    private readonly List<FakeTransaction> _transactions = new();
    private int _artificialDelayMs = 0;

    /// <summary>
    /// Gets the history of query specifications
    /// </summary>
    public IReadOnlyList<QuerySpec> QueryHistory => _queryHistory.AsReadOnly();

    /// <summary>
    /// Gets the history of command specifications
    /// </summary>
    public IReadOnlyList<CommandSpec> CommandHistory => _commandHistory.AsReadOnly();

    /// <summary>
    /// Gets the method call counts
    /// </summary>
    public IReadOnlyDictionary<string, int> MethodCallCounts => _methodCallCounts.AsReadOnly();

    /// <summary>
    /// Gets the notifications that were raised
    /// </summary>
    public IReadOnlyList<DbNotification> Notifications => _notifications.AsReadOnly();

    /// <summary>
    /// Gets the active transactions
    /// </summary>
    public IReadOnlyList<FakeTransaction> Transactions => _transactions.AsReadOnly();

    /// <summary>
    /// Sets artificial delay for operations
    /// </summary>
    public int ArtificialDelayMs
    {
        get => _artificialDelayMs;
        set => _artificialDelayMs = Math.Max(0, value);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<int> ExecuteCommandAsync(CommandSpec spec, CancellationToken ct = default)
    {
        IncrementCallCount(nameof(ExecuteCommandAsync));
        _commandHistory.Add(spec);
        
        if (_artificialDelayMs > 0)
        {
            await Task.Delay(_artificialDelayMs, ct);
        }

        ct.ThrowIfCancellationRequested();
        return 1; // Simulate one affected row
    }

    /// <inheritdoc />
    public Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct = default)
    {
        IncrementCallCount(nameof(BeginTransactionAsync));
        var transaction = new FakeTransaction();
        _transactions.Add(transaction);
        return Task.FromResult<IAsyncDisposable>(transaction);
    }

    /// <inheritdoc />
    public event EventHandler<DbNotification>? Notified;

    /// <summary>
    /// Raises a notification event
    /// </summary>
    /// <param name="notification">Notification to raise</param>
    public void RaiseNotification(DbNotification notification)
    {
        _notifications.Add(notification);
        Notified?.Invoke(this, notification);
    }

    /// <summary>
    /// Clears all history and counters
    /// </summary>
    public void Clear()
    {
        _queryHistory.Clear();
        _commandHistory.Clear();
        _methodCallCounts.Clear();
        _notifications.Clear();
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

/// <summary>
/// Fake transaction implementation
/// </summary>
public class FakeTransaction : IAsyncDisposable
{
    /// <summary>
    /// Gets whether the transaction was committed
    /// </summary>
    public bool IsCommitted { get; private set; }

    /// <summary>
    /// Gets whether the transaction was rolled back
    /// </summary>
    public bool IsRolledBack { get; private set; }

    /// <summary>
    /// Gets whether the transaction is disposed
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Commits the transaction
    /// </summary>
    public void Commit()
    {
        if (IsDisposed)
            throw new InvalidOperationException("Transaction is already disposed");
        if (IsRolledBack)
            throw new InvalidOperationException("Cannot commit after rollback");
        
        IsCommitted = true;
    }

    /// <summary>
    /// Rolls back the transaction
    /// </summary>
    public void Rollback()
    {
        if (IsDisposed)
            throw new InvalidOperationException("Transaction is already disposed");
        
        IsRolledBack = true;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
