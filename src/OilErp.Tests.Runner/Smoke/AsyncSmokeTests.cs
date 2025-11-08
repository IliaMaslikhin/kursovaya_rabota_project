using OilErp.Core.Dto;
using OilErp.Tests.Runner.TestDoubles;
using OilErp.Tests.Runner.Util;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// Async and resiliency checks (cancellation, timeout, fake instrumentation).
/// </summary>
public class AsyncSmokeTests
{
    /// <summary>
    /// Cancels a long-running pg_sleep call through StorageAdapter to ensure cancellation flows end-to-end.
    /// </summary>
    public async Task<TestResult> TestDbCancellationOnPgSleep()
    {
        const string testName = "Db_Cancellation_On_PgSleep";
        try
        {
            var storage = TestEnvironment.CreateStorageAdapter();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var spec = new QuerySpec("pg_catalog.pg_sleep", new Dictionary<string, object?> { ["seconds"] = 5d });
            try
            {
                await storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec, cts.Token);
                return new TestResult(testName, false, "pg_sleep should have been cancelled");
            }
            catch (OperationCanceledException)
            {
                return new TestResult(testName, true);
            }
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }

    /// <summary>
    /// Verifies command timeout by invoking pg_sleep with a 1-second limit.
    /// </summary>
    public async Task<TestResult> TestDbTimeoutOnPgSleep()
    {
        const string testName = "Db_Timeout_On_PgSleep";
        try
        {
            var storage = TestEnvironment.CreateStorageAdapter();
            var spec = new QuerySpec("pg_catalog.pg_sleep", new Dictionary<string, object?> { ["seconds"] = 5d }, TimeoutSeconds: 1);
            try
            {
                await storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec);
                return new TestResult(testName, false, "Expected timeout but query succeeded");
            }
            catch (Exception)
            {
                return new TestResult(testName, true);
            }
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }

    /// <summary>
    /// Uses the fake storage port to ensure concurrent commands are tracked (health check for instrumentation).
    /// </summary>
    public async Task<TestResult> TestFakeStorageConcurrentCommandsCounter()
    {
        const string testName = "Fake_Concurrent_Commands_Counter";
        try
        {
            var storage = new FakeStoragePort { ArtificialDelayMs = 10 };
            var spec = new CommandSpec("fake.op", new Dictionary<string, object?>());
            var tasks = Enumerable.Range(0, 10).Select(_ => storage.ExecuteCommandAsync(spec)).ToArray();
            await Task.WhenAll(tasks);

            var calls = storage.MethodCallCounts.TryGetValue(nameof(storage.ExecuteCommandAsync), out var value) ? value : 0;
            if (calls != 10)
            {
                return new TestResult(testName, false, $"Expected 10 calls, got {calls}");
            }

            return new TestResult(testName, true);
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }

    /// <summary>
    /// Validates that fake storage notifications propagate payloads to subscribers.
    /// </summary>
    public Task<TestResult> TestFakeStorageNotificationBroadcast()
    {
        const string testName = "Fake_Storage_Notification_Broadcast";
        try
        {
            var storage = new FakeStoragePort();
            DbNotification? received = null;
            storage.Notified += (_, notification) => received = notification;

            var payload = new DbNotification("hc_channel", "{\"ok\":true}", ProcessId: 1234);
            storage.RaiseNotification(payload);

            if (received == null || received.Channel != payload.Channel || received.Payload != payload.Payload)
            {
                return Task.FromResult(new TestResult(testName, false, "Notification payload mismatch"));
            }

            return Task.FromResult(new TestResult(testName, true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult(testName, false, ex.Message));
        }
    }
}
