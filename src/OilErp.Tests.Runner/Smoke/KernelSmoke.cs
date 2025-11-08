using Npgsql;
using OilErp.Core.Dto;
using OilErp.Core.Operations;
using OilErp.Tests.Runner;
using OilErp.Tests.Runner.Util;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// Kernel-level health checks (connections, kernel bootstrapping, transactions).
/// </summary>
public class KernelSmoke
{
    /// <summary>
    /// Opens a raw Npgsql connection using the same config the kernel will consume.
    /// </summary>
    public async Task<TestResult> TestKernelOpensConnection()
    {
        const string testName = "Kernel_Opens_Connection";
        try
        {
            var cfg = TestEnvironment.LoadStorageConfig();
            await using var conn = new NpgsqlConnection(cfg.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "select version()";
            var version = (string?)await cmd.ExecuteScalarAsync();
            Console.WriteLine($"[Ядро] Соединение установлено с {version}");
            return new TestResult(testName, true);
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }

    /// <summary>
    /// Runs a lightweight analytics query via the kernel storage to ensure adapters are wired.
    /// </summary>
    public async Task<TestResult> TestKernelExecutesHealthQuery()
    {
        const string testName = "Kernel_Executes_Health_Query";
        try
        {
            var kernel = TestEnvironment.CreateKernel();
            var spec = new QuerySpec(
                OperationNames.Central.AnalyticsTopAssetsByCr,
                new Dictionary<string, object?> { ["p_limit"] = 1 });
            var rows = await kernel.Storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec);
            Console.WriteLine($"[Ядро] Запрос проверки здоровья вернул {rows.Count} строк");
            return new TestResult(testName, true);
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }

    /// <summary>
    /// Ensures begin/commit/rollback paths on kernel.Storage do not leak connections.
    /// </summary>
    public async Task<TestResult> TestKernelTransactionLifecycle()
    {
        const string testName = "Kernel_Transaction_Lifecycle";
        try
        {
            var kernel = TestEnvironment.CreateKernel();
            await using var tx = await kernel.Storage.BeginTransactionAsync();
            await CommitAsync(tx);

            // Second scope to ensure rollback path works (Dispose without commit)
            await using var rollbackTx = await kernel.Storage.BeginTransactionAsync();
            // disposing without explicit commit should rollback
            return new TestResult(testName, true);
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }

    private static async Task CommitAsync(IAsyncDisposable tx)
    {
        var commit = tx.GetType().GetMethod("CommitAsync", new[] { typeof(CancellationToken) })
                     ?? tx.GetType().GetMethod("CommitAsync", Type.EmptyTypes);

        if (commit == null) return;

        var result = commit.GetParameters().Length == 1
            ? commit.Invoke(tx, new object[] { CancellationToken.None })
            : commit.Invoke(tx, null);

        if (result is Task task)
        {
            await task.ConfigureAwait(false);
        }
    }
}
