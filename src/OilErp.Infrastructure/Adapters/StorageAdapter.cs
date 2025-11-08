using System.Text.Json;
using OilErp.Core.Abstractions;
using OilErp.Core.Dto;
using OilErp.Infrastructure.Config;
using Npgsql;
using NpgsqlTypes;

namespace OilErp.Infrastructure.Adapters;

/// <summary>
/// PostgreSQL-реализация IStoragePort на базе Npgsql
/// </summary>
public class StorageAdapter : DbClientBase
{
    private readonly StorageConfig _config;

    private static readonly AsyncLocal<PgTransactionScope?> _currentTx = new();

    private NpgsqlConnection? _listenConn;
    private readonly HashSet<string> _listenChannels = new(StringComparer.Ordinal);
    private CancellationTokenSource? _listenCts;
    private Task? _listenTask;

    public StorageAdapter() : this(BuildConfigFromEnv()) { }

    public StorageAdapter(StorageConfig config)
    {
        _config = config;
    }

    private static StorageConfig BuildConfigFromEnv()
    {
        var cs = Environment.GetEnvironmentVariable("OIL_ERP_PG")
                 ?? "Host=localhost;Username=postgres;Password=postgres;Database=postgres";
        var timeout = int.TryParse(Environment.GetEnvironmentVariable("OIL_ERP_PG_TIMEOUT"), out var t) ? t : 30;
        return new StorageConfig(cs, timeout);
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<T>> ExecuteQueryAsync<T>(QuerySpec spec, CancellationToken ct = default)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));

        var (schema, name) = SplitQualifiedName(spec.OperationName);
        var conn = await GetConnectionAsync(ct);
        await using var _ = conn.Item2; // ensures disposal if we opened a new connection
        var npg = conn.Item1;

        var meta = await GetRoutineMetadataAsync(schema, name, npg, spec.Parameters?.Count ?? 0, ct);

        if (meta.IsProcedure)
        {
            // Процедуры не возвращают набор; вызов через CALL и вернуть пустой список
            var cmd = BuildRoutineCommand(npg, meta, spec.Parameters, isQuery: false);
            cmd.CommandTimeout = ResolveTimeout(spec.TimeoutSeconds);
            await cmd.ExecuteNonQueryAsync(ct);
            return Array.Empty<T>();
        }

        // FUNCTION: определить канал возврата
        var queryCmd = BuildRoutineCommand(npg, meta, spec.Parameters, isQuery: true);
        queryCmd.CommandTimeout = ResolveTimeout(spec.TimeoutSeconds);

        if (meta.ReturnsJson)
        {
            var result = await queryCmd.ExecuteScalarAsync(ct);
            var text = result?.ToString() ?? string.Empty;
            var list = new List<T>(1);
            if (text is T t) list.Add(t);
            else if (typeof(T) == typeof(object)) list.Add((T)(object)text);
            else throw new InvalidCastException($"Cannot cast JSON text result to {typeof(T).Name}");
            return list;
        }
        else
        {
            await using var reader = await queryCmd.ExecuteReaderAsync(ct);
            var rows = new List<object>();
            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.Ordinal);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var val = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
                    row[reader.GetName(i)] = val;
                }
                rows.Add(row);
            }

            // Приведение к запрошенному T
            var casted = new List<T>(rows.Count);
            foreach (var r in rows)
            {
                if (r is T ok)
                {
                    casted.Add(ok);
                }
                else if (typeof(T) == typeof(object))
                {
                    casted.Add((T)r);
                }
                else
                {
                    throw new InvalidCastException($"Row type {r.GetType().Name} is not assignable to {typeof(T).Name}");
                }
            }
            return casted;
        }
    }

    /// <inheritdoc />
    public override async Task<int> ExecuteCommandAsync(CommandSpec spec, CancellationToken ct = default)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));
        var (schema, name) = SplitQualifiedName(spec.OperationName);
        var conn = await GetConnectionAsync(ct);
        await using var _ = conn.Item2;
        var npg = conn.Item1;

        var meta = await GetRoutineMetadataAsync(schema, name, npg, spec.Parameters?.Count ?? 0, ct);
        var cmd = BuildRoutineCommand(npg, meta, spec.Parameters, isQuery: !meta.IsProcedure);
        cmd.CommandTimeout = ResolveTimeout(spec.TimeoutSeconds);

        try
        {
            if (meta.IsProcedure)
            {
                var affected = await cmd.ExecuteNonQueryAsync(ct);
                return affected < 0 ? 0 : affected;
            }
            else
            {
                // Функции: выполнить SELECT. Если вернули число — трактуем как affected; иначе 1 при успехе
                var result = await cmd.ExecuteScalarAsync(ct);
                if (result is sbyte or byte or short or ushort or int or uint or long or ulong)
                    return Convert.ToInt32(result);
                return 1;
            }
        }
        catch (PostgresException pg)
        {
            // Пробрасываем SQLSTATE в тексте исключения
            throw new InvalidOperationException($"PG error {pg.SqlState}: {pg.MessageText}", pg);
        }
    }

    /// <inheritdoc />
    public override async Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTx.Value != null)
            throw new InvalidOperationException("Transaction already started in this async context");

        var conn = new NpgsqlConnection(_config.ConnectionString);
        await conn.OpenAsync(ct);
        var tx = await conn.BeginTransactionAsync(ct);
        var scope = new PgTransactionScope(conn, tx, ClearCurrentTx);
        _currentTx.Value = scope;
        return scope;
    }

    /// <inheritdoc />
    public override event EventHandler<DbNotification>? Notified;

    /// <summary>
    /// Подписка на LISTEN канал (соединение живёт в фоне)
    /// </summary>
    public async Task SubscribeAsync(string channel, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(channel)) throw new ArgumentNullException(nameof(channel));
        await EnsureListenerAsync(ct);
        if (_listenConn == null) throw new InvalidOperationException("Listener not initialized");
        await using var cmd = _listenConn.CreateCommand();
        cmd.CommandText = $"LISTEN \"{channel}\"";
        await cmd.ExecuteNonQueryAsync(ct);
        _listenChannels.Add(channel);
    }

    /// <summary>
    /// Отписка от LISTEN канала
    /// </summary>
    public async Task UnsubscribeAsync(string channel, CancellationToken ct = default)
    {
        if (_listenConn == null) return;
        await using var cmd = _listenConn.CreateCommand();
        cmd.CommandText = $"UNLISTEN \"{channel}\"";
        await cmd.ExecuteNonQueryAsync(ct);
        _listenChannels.Remove(channel);
    }

    private void ClearCurrentTx()
    {
        _currentTx.Value = null;
    }

    private async Task EnsureListenerAsync(CancellationToken ct)
    {
        if (_listenConn != null && _listenConn.FullState == System.Data.ConnectionState.Open) return;
        _listenConn = new NpgsqlConnection(_config.ConnectionString);
        _listenConn.Notification += (_, e) => Notified?.Invoke(this, new DbNotification(e.Channel, e.Payload, e.PID));
        await _listenConn.OpenAsync(ct);

        _listenCts?.Cancel();
        _listenCts = new CancellationTokenSource();
        var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _listenCts.Token);
        _listenTask = Task.Run(async () =>
        {
            try
            {
                while (!linked.IsCancellationRequested)
                {
                    await _listenConn.WaitAsync(linked.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch
            {
                // swallow background errors; notifications are best-effort
            }
        }, linked.Token);
    }

    private int ResolveTimeout(int? specTimeout) => specTimeout ?? _config.CommandTimeoutSeconds;

    private static (string Schema, string Name) SplitQualifiedName(string op)
    {
        if (string.IsNullOrWhiteSpace(op)) throw new ArgumentException("Operation name is required", nameof(op));
        var parts = op.Split('.', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : ("public", parts[0]);
    }

    private async Task<(NpgsqlConnection, IAsyncDisposable?)> GetConnectionAsync(CancellationToken ct)
    {
        if (_currentTx.Value != null)
        {
            return (_currentTx.Value.Connection, null);
        }
        var conn = new NpgsqlConnection(_config.ConnectionString);
        await conn.OpenAsync(ct);
        return (conn, conn);
    }

    private static bool LooksLikeJsonParam(string paramName)
    {
        if (string.IsNullOrEmpty(paramName)) return false;
        paramName = paramName.ToLowerInvariant();
        return paramName.Contains("json") || paramName.Contains("payload") || paramName.Contains("points") || paramName.Contains("data");
    }

    private NpgsqlCommand BuildRoutineCommand(NpgsqlConnection conn, RoutineMetadata meta, IReadOnlyDictionary<string, object?>? parameters, bool isQuery)
    {
        var cmd = conn.CreateCommand();
        parameters ??= new Dictionary<string, object?>();

        // Построить список именованных аргументов: argname => @argname
        var args = string.Join(", ", parameters.Select(kv => $"{kv.Key} => @{kv.Key}"));

        if (meta.IsProcedure)
        {
            cmd.CommandText = args.Length == 0
                ? $"call \"{meta.Schema}\".\"{meta.Name}\"()"
                : $"call \"{meta.Schema}\".\"{meta.Name}\"({args})";
        }
        else
        {
            var selectExpr = meta.ReturnsJson
                ? $"\"{meta.Schema}\".\"{meta.Name}\"({args})::text"
                : meta.ReturnsSet
                    ? $"* from \"{meta.Schema}\".\"{meta.Name}\"({args})"
                    : $"\"{meta.Schema}\".\"{meta.Name}\"({args})";

            cmd.CommandText = meta.ReturnsSet
                ? $"select {selectExpr}"
                : $"select {selectExpr}";
        }

        foreach (var (key, value) in parameters)
        {
            var p = cmd.Parameters.Add(new NpgsqlParameter($"@{key}", value ?? DBNull.Value));
            if (value is null)
            {
                // leave db type null
            }
            else if (LooksLikeJsonParam(key))
            {
                p.NpgsqlDbType = NpgsqlDbType.Jsonb;
                if (value is not string)
                {
                    p.Value = JsonSerializer.Serialize(value);
                }
            }
            else if (value is DateTimeOffset dto)
            {
                p.Value = dto.UtcDateTime;
            }
            else if (value is JsonElement je)
            {
                p.NpgsqlDbType = NpgsqlDbType.Jsonb;
                p.Value = je.GetRawText();
            }
            // otherwise let Npgsql infer type
        }

        if (_currentTx.Value?.Transaction != null)
        {
            cmd.Transaction = _currentTx.Value.Transaction;
        }
        return cmd;
    }

    private static async Task<RoutineMetadata> GetRoutineMetadataAsync(string schema, string name, NpgsqlConnection conn, int argCount, CancellationToken ct)
    {
        const string sql = @"select p.prokind, p.proretset, rt.typname as rettype
from pg_proc p
join pg_namespace n on n.oid = p.pronamespace
join pg_type rt on rt.oid = p.prorettype
where n.nspname = @schema and p.proname = @name
order by p.oid desc limit 1";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@name", name);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException($"Routine not found: {schema}.{name}");

        var prokind = reader.GetString(0); // f=function, p=procedure
        var proretset = reader.GetBoolean(1);
        var rettype = reader.GetString(2);
        return new RoutineMetadata(schema, name, prokind == "p", proretset, rettype);
    }

    private readonly record struct RoutineMetadata(string Schema, string Name, bool IsProcedure, bool ReturnsSet, string ReturnType)
    {
        public bool ReturnsJson => string.Equals(ReturnType, "json", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(ReturnType, "jsonb", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Обертка над NpgsqlTransaction с асинхронными Commit/Rollback и сейвпоинтами
    /// </summary>
    private sealed class PgTransactionScope : IAsyncDisposable
    {
        public NpgsqlConnection Connection { get; }
        public NpgsqlTransaction Transaction { get; }
        private readonly Action _onDispose;
        private bool _completed;

        public PgTransactionScope(NpgsqlConnection conn, NpgsqlTransaction tx, Action onDispose)
        {
            Connection = conn;
            Transaction = tx;
            _onDispose = onDispose;
        }

        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (_completed) return;
            await Transaction.CommitAsync(ct);
            _completed = true;
        }

        public async Task RollbackAsync(CancellationToken ct = default)
        {
            if (_completed) return;
            await Transaction.RollbackAsync(ct);
            _completed = true;
        }

        public async Task CreateSavepointAsync(string name, CancellationToken ct = default)
        {
            await ExecuteAsync($"SAVEPOINT \"{name}\"", ct);
        }

        public async Task RollbackToSavepointAsync(string name, CancellationToken ct = default)
        {
            await ExecuteAsync($"ROLLBACK TO SAVEPOINT \"{name}\"", ct);
        }

        public async Task ReleaseSavepointAsync(string name, CancellationToken ct = default)
        {
            await ExecuteAsync($"RELEASE SAVEPOINT \"{name}\"", ct);
        }

        private async Task ExecuteAsync(string sql, CancellationToken ct)
        {
            await using var cmd = Connection.CreateCommand();
            cmd.Transaction = Transaction;
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!_completed)
                {
                    await Transaction.RollbackAsync();
                }
            }
            catch { /* ignore */ }
            finally
            {
                await Transaction.DisposeAsync();
                await Connection.CloseAsync();
                await Connection.DisposeAsync();
                _onDispose();
            }
        }
    }
}
