using OilErp.Core.Dto;
using OilErp.Core.Operations;
using OilErp.Tests.Runner.TestDoubles;
using OilErp.Tests.Runner.Util;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// Async smoke tests for cancellation and timeout scenarios
/// </summary>
public class AsyncSmokeTests
{
    /// <summary>
    /// Tests that CancellationToken propagates to query
    /// </summary>
    /// <returns>Test result</returns>
    public async Task<TestResult> TestCancellationTokenPropagatesToQuery()
    {
        try
        {
            var storage = new FakeStoragePort();
            var cts = new CancellationTokenSource();
            var querySpec = new QuerySpec(OperationNames.Central.AnalyticsAssetSummary, new Dictionary<string, object?>());
            
            cts.Cancel();
            
            try
            {
                await storage.ExecuteQueryAsync<object>(querySpec, cts.Token);
                return new TestResult("CancellationToken_Propagates_To_Query", false, "Query should have been cancelled");
            }
            catch (OperationCanceledException)
            {
                return new TestResult("CancellationToken_Propagates_To_Query", true);
            }
        }
        catch (Exception ex)
        {
            return new TestResult("CancellationToken_Propagates_To_Query", false, ex.Message);
        }
    }

    /// <summary>
    /// Tests that CancellationToken propagates to command
    /// </summary>
    /// <returns>Test result</returns>
    public async Task<TestResult> TestCancellationTokenPropagatesToCommand()
    {
        try
        {
            var storage = new FakeStoragePort();
            var cts = new CancellationTokenSource();
            var commandSpec = new CommandSpec(OperationNames.Plant.MeasurementsInsertBatch, new Dictionary<string, object?>());
            
            cts.Cancel();
            
            try
            {
                await storage.ExecuteCommandAsync(commandSpec, cts.Token);
                return new TestResult("CancellationToken_Propagates_To_Command", false, "Command should have been cancelled");
            }
            catch (OperationCanceledException)
            {
                return new TestResult("CancellationToken_Propagates_To_Command", true);
            }
        }
        catch (Exception ex)
        {
            return new TestResult("CancellationToken_Propagates_To_Command", false, ex.Message);
        }
    }

    /// <summary>
    /// Tests that timeout on query is observed by adapter
    /// </summary>
    /// <returns>Test result</returns>
    public async Task<TestResult> TestTimeoutOnQueryIsObservedByAdapter()
    {
        try
        {
            var storage = new FakeStoragePort();
            var querySpec = new QuerySpec(OperationNames.Central.AnalyticsAssetSummary, new Dictionary<string, object?>(), TimeoutSeconds: 1);
            
            await storage.ExecuteQueryAsync<object>(querySpec);
            
            // Check that the timeout was recorded in the query history
            var lastQuery = storage.QueryHistory.LastOrDefault();
            if (lastQuery?.TimeoutSeconds != 1)
            {
                return new TestResult("Timeout_On_Query_Is_Observed_By_Adapter", false, "Timeout not recorded in query spec");
            }
            
            return new TestResult("Timeout_On_Query_Is_Observed_By_Adapter", true);
        }
        catch (Exception ex)
        {
            return new TestResult("Timeout_On_Query_Is_Observed_By_Adapter", false, ex.Message);
        }
    }

    /// <summary>
    /// Tests that timeout on command is observed by adapter
    /// </summary>
    /// <returns>Test result</returns>
    public async Task<TestResult> TestTimeoutOnCommandIsObservedByAdapter()
    {
        try
        {
            var storage = new FakeStoragePort();
            var commandSpec = new CommandSpec(OperationNames.Plant.MeasurementsInsertBatch, new Dictionary<string, object?>(), TimeoutSeconds: 2);
            
            await storage.ExecuteCommandAsync(commandSpec);
            
            // Check that the timeout was recorded in the command history
            var lastCommand = storage.CommandHistory.LastOrDefault();
            if (lastCommand?.TimeoutSeconds != 2)
            {
                return new TestResult("Timeout_On_Command_Is_Observed_By_Adapter", false, "Timeout not recorded in command spec");
            }
            
            return new TestResult("Timeout_On_Command_Is_Observed_By_Adapter", true);
        }
        catch (Exception ex)
        {
            return new TestResult("Timeout_On_Command_Is_Observed_By_Adapter", false, ex.Message);
        }
    }

    /// <summary>
    /// Tests that concurrent commands counter is accurate
    /// </summary>
    /// <returns>Test result</returns>
    public async Task<TestResult> TestConcurrentCommandsCounterIsAccurate()
    {
        try
        {
            var storage = new FakeStoragePort();
            var commandSpec = new CommandSpec(OperationNames.Plant.MeasurementsInsertBatch, new Dictionary<string, object?>());
            
            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(storage.ExecuteCommandAsync(commandSpec));
            }
            
            await Task.WhenAll(tasks);
            
            if (storage.MethodCallCounts.GetValueOrDefault(nameof(storage.ExecuteCommandAsync), 0) != 5)
            {
                return new TestResult("Concurrent_Commands_Counter_Is_Accurate", false, $"Expected 5 calls, got {storage.MethodCallCounts.GetValueOrDefault(nameof(storage.ExecuteCommandAsync), 0)}");
            }
            
            return new TestResult("Concurrent_Commands_Counter_Is_Accurate", true);
        }
        catch (Exception ex)
        {
            return new TestResult("Concurrent_Commands_Counter_Is_Accurate", false, ex.Message);
        }
    }

    /// <summary>
    /// Tests that event raise notified delivers payload
    /// </summary>
    /// <returns>Test result</returns>
    public Task<TestResult> TestEventRaiseNotifiedDeliversPayload()
    {
        try
        {
            var storage = new FakeStoragePort();
            var receivedNotification = (DbNotification?)null;
            
            storage.Notified += (sender, args) => receivedNotification = args;
            
            var testNotification = new DbNotification("test_channel", "test_payload", 123);
            storage.RaiseNotification(testNotification);
            
            if (receivedNotification == null)
            {
                return Task.FromResult(new TestResult("Event_Raise_Notified_Delivers_Payload", false, "No notification received"));
            }
            
            if (receivedNotification.Channel != "test_channel" || receivedNotification.Payload != "test_payload")
            {
                return Task.FromResult(new TestResult("Event_Raise_Notified_Delivers_Payload", false, "Notification payload incorrect"));
            }
            
            return Task.FromResult(new TestResult("Event_Raise_Notified_Delivers_Payload", true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult("Event_Raise_Notified_Delivers_Payload", false, ex.Message));
        }
    }

    /// <summary>
    /// Tests that NotImplemented adapter methods fail as expected
    /// </summary>
    /// <returns>Test result</returns>
    public Task<TestResult> TestNotImplementedAdapterMethodsFailAsExpected()
    {
        try
        {
            // Use the real StorageAdapter that throws NotImplementedException
            var storage = new OilErp.Infrastructure.Adapters.StorageAdapter();
            var querySpec = new QuerySpec(OperationNames.Central.AnalyticsAssetSummary, new Dictionary<string, object?>());
            var commandSpec = new CommandSpec(OperationNames.Plant.MeasurementsInsertBatch, new Dictionary<string, object?>());
            
            // Test ExecuteQueryAsync throws NotImplementedException
            try
            {
                storage.ExecuteQueryAsync<object>(querySpec).Wait();
                return Task.FromResult(new TestResult("NotImplemented_Adapter_Methods_Fail_As_Expected", false, "ExecuteQueryAsync should throw NotImplementedException"));
            }
            catch (Exception ex) when (ex is NotImplementedException || ex.InnerException is NotImplementedException)
            {
                // Expected
            }
            
            // Test ExecuteCommandAsync throws NotImplementedException
            try
            {
                storage.ExecuteCommandAsync(commandSpec).Wait();
                return Task.FromResult(new TestResult("NotImplemented_Adapter_Methods_Fail_As_Expected", false, "ExecuteCommandAsync should throw NotImplementedException"));
            }
            catch (Exception ex) when (ex is NotImplementedException || ex.InnerException is NotImplementedException)
            {
                // Expected
            }
            
            // Test BeginTransactionAsync throws NotImplementedException
            try
            {
                storage.BeginTransactionAsync().Wait();
                return Task.FromResult(new TestResult("NotImplemented_Adapter_Methods_Fail_As_Expected", false, "BeginTransactionAsync should throw NotImplementedException"));
            }
            catch (Exception ex) when (ex is NotImplementedException || ex.InnerException is NotImplementedException)
            {
                // Expected
            }
            
            return Task.FromResult(new TestResult("NotImplemented_Adapter_Methods_Fail_As_Expected", true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult("NotImplemented_Adapter_Methods_Fail_As_Expected", false, ex.Message));
        }
    }
}
