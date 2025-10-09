using OilErp.Core.Dto;
using OilErp.Core.Operations;
using OilErp.Infrastructure.Adapters;
using OilErp.Tests.Runner.TestDoubles;
using OilErp.Tests.Runner.Util;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// Smoke tests for storage functionality
/// </summary>
public class StorageSmoke
{
    /// <summary>
    /// Tests that all IStoragePort methods throw NotImplementedException
    /// </summary>
    /// <returns>Test result</returns>
    public Task<TestResult> TestStorageMethodsNotImplemented()
    {
        try
        {
            var storage = new StorageAdapter();
            var querySpec = new QuerySpec(
                OperationNames.Central.AnalyticsAssetSummary,
                new Dictionary<string, object?> { ["asset_code"] = "TEST" }
            );
            var commandSpec = new CommandSpec(
                OperationNames.Plant.MeasurementsInsertBatch,
                new Dictionary<string, object?> { ["asset_code"] = "TEST" }
            );

            // Test ExecuteQueryAsync
            try
            {
                storage.ExecuteQueryAsync<object>(querySpec).Wait();
                return Task.FromResult(new TestResult("Storage_Methods_NotImplemented", false, "ExecuteQueryAsync did not throw"));
            }
            catch (NotImplementedException)
            {
                // Expected
            }

            // Test ExecuteCommandAsync
            try
            {
                storage.ExecuteCommandAsync(commandSpec).Wait();
                return Task.FromResult(new TestResult("Storage_Methods_NotImplemented", false, "ExecuteCommandAsync did not throw"));
            }
            catch (NotImplementedException)
            {
                // Expected
            }

            // Test BeginTransactionAsync
            try
            {
                storage.BeginTransactionAsync().Wait();
                return Task.FromResult(new TestResult("Storage_Methods_NotImplemented", false, "BeginTransactionAsync did not throw"));
            }
            catch (NotImplementedException)
            {
                // Expected
            }

            return Task.FromResult(new TestResult("Storage_Methods_NotImplemented", true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult("Storage_Methods_NotImplemented", false, ex.Message));
        }
    }

    /// <summary>
    /// Tests that subscribing/unsubscribing to Notified event does not throw
    /// </summary>
    /// <returns>Test result</returns>
    public Task<TestResult> TestStorageSubscribeNotifications()
    {
        try
        {
            var storage = new StorageAdapter();
            
            // Subscribe
            storage.Notified += (sender, args) => { };
            
            // Unsubscribe
            storage.Notified -= (sender, args) => { };

            return Task.FromResult(new TestResult("Storage_Subscribe_Notifications", true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult("Storage_Subscribe_Notifications", false, ex.Message));
        }
    }
}