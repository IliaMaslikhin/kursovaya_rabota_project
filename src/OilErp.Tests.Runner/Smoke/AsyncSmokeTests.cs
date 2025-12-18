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
}
