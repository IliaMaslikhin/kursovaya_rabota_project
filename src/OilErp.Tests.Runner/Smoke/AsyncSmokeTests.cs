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

    /// <summary>
    /// Ensures subscribe/unsubscribe calls are routed through the storage port interface.
    /// </summary>
    public async Task<TestResult> TestFakeStorageSubscribeUnsubscribe()
    {
        const string testName = "Fake_Storage_Subscribe_Unsubscribe";
        try
        {
            var storage = new FakeStoragePort();
            await storage.SubscribeAsync("hc_channel");
            await storage.UnsubscribeAsync("hc_channel");

            var subs = storage.MethodCallCounts.TryGetValue(nameof(storage.SubscribeAsync), out var s) ? s : 0;
            var unsubs = storage.MethodCallCounts.TryGetValue(nameof(storage.UnsubscribeAsync), out var u) ? u : 0;

            if (subs != 1 || unsubs != 1)
            {
                return new TestResult(testName, false, $"subscribe={subs} unsubscribe={unsubs}");
            }

            return new TestResult(testName, true);
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }

    /// <summary>
    /// LISTEN/NOTIFY круговой тест через StorageAdapter.
    /// </summary>
    public async Task<TestResult> TestListenNotifyRoundtrip()
    {
        const string testName = "Listen_Notify_Roundtrip";
        try
        {
            var storage = TestEnvironment.CreateStorageAdapter();
            var channel = $"hc_roundtrip_{Guid.NewGuid():N}";
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Handler(object? _, DbNotification n)
            {
                if (string.Equals(n.Channel, channel, StringComparison.OrdinalIgnoreCase))
                    tcs.TrySetResult(true);
            }

            storage.Notified += Handler;
            await storage.SubscribeAsync(channel);
            try
            {
                var spec = new CommandSpec("pg_catalog.pg_notify", new Dictionary<string, object?>
                {
                    ["channel"] = channel,
                    ["payload"] = "{\"ok\":true}"
                });
                await storage.ExecuteCommandAsync(spec);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await using var reg = cts.Token.Register(() => tcs.TrySetCanceled(cts.Token));
                var ok = await tcs.Task;
                return ok ? new TestResult(testName, true) : new TestResult(testName, false, "Уведомление не получено");
            }
            finally
            {
                await storage.UnsubscribeAsync(channel);
                storage.Notified -= Handler;
            }
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }
}
