using OilErp.Core.Dto;
using OilErp.Core.Operations;
using OilErp.Infrastructure.Adapters;
using OilErp.Tests.Runner.Util;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// Smoke tests for storage functionality
/// </summary>
public class StorageSmoke
{
    /// <summary>
    /// Простой Query к реальной БД (top-by-cr limit 1)
    /// </summary>
    public async Task<TestResult> TestDbQueryTopByCr()
    {
        try
        {
            var storage = new StorageAdapter();
            var spec = new QuerySpec(OperationNames.Central.AnalyticsTopAssetsByCr, new Dictionary<string, object?> { ["p_limit"] = 1 });
            var rows = await storage.ExecuteQueryAsync<Dictionary<string, object?>>(spec);
            return new TestResult("Db_Query_TopByCr", true);
        }
        catch (Exception ex)
        {
            return new TestResult("Db_Query_TopByCr", false, ex.Message);
        }
    }

    /// <summary>
    /// Простой Command к реальной БД (enqueue event)
    /// </summary>
    public async Task<TestResult> TestDbCommandEnqueue()
    {
        try
        {
            var storage = new StorageAdapter();
            var spec = new CommandSpec(
                OperationNames.Central.EventsEnqueue,
                new Dictionary<string, object?>
                {
                    ["p_event_type"] = "SMOKE_TEST",
                    ["p_source_plant"] = "ANPZ",
                    ["p_payload"] = "{\"hello\":\"world\"}"
                }
            );
            var affected = await storage.ExecuteCommandAsync(spec);
            return new TestResult("Db_Command_Enqueue", true);
        }
        catch (Exception ex)
        {
            return new TestResult("Db_Command_Enqueue", false, ex.Message);
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
