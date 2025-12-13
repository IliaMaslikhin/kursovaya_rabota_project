using System;
using System.Threading.Tasks;
using OilErp.Infrastructure.Adapters;
using OilErp.Tests.Runner.Util;
using OilErp.Core.Services.Central;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// Нагрузочные/массовые сценарии с откатом.
/// </summary>
public class LoadSmokeTests
{
    private const string EnableEnv = "OILERP__TESTS__ENABLE_LOAD";

    /// <summary>
    /// Генерирует большую пачку событий и прогоняет fn_ingest_events внутри транзакции с откатом.
    /// </summary>
    public async Task<TestResult> TestBulkEventsRollbackSafe()
    {
        const string testName = "Bulk_Events_Rollback_Safe";
        if (!ShouldRunLoadTests())
        {
            return new TestResult(testName, true, "Skipped heavy load test (set OILERP__TESTS__ENABLE_LOAD=1 to enable)", true);
        }

        try
        {
            var storage = TestEnvironment.CreateStorageAdapter();
            await using var tx = await storage.BeginTransactionAsync();

            var insertSpec = new CommandSpec(
                "public.fn_events_enqueue",
                new Dictionary<string, object?>
                {
                    ["p_event_type"] = "HC_MEASUREMENT_BATCH",
                    ["p_source_plant"] = "ANPZ",
                    ["p_payload"] = GeneratePayloadJson()
                },
                TimeoutSeconds: 300);

            var events = ResolveEventsCount();
            for (int i = 0; i < events; i++)
            {
                await storage.ExecuteCommandAsync(insertSpec);
            }

            var ingest = new FnIngestEventsService(storage);
            var processed = await ingest.fn_ingest_eventsAsync(events, CancellationToken.None);

            await tx.RollbackAsync();
            return processed > 0
                ? new TestResult(testName, true, $"processed={processed}")
                : new TestResult(testName, false, "ingest processed 0 rows");
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }

    private static string GeneratePayloadJson()
    {
        var now = DateTime.UtcNow;
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            asset_code = "LOAD_TEST",
            prev_thk = 10.0m,
            prev_date = now.AddDays(-30),
            last_thk = 9.0m,
            last_date = now
        });
    }

    private static bool ShouldRunLoadTests()
    {
        var flag = Environment.GetEnvironmentVariable(EnableEnv);
        if (string.IsNullOrWhiteSpace(flag)) return false;
        return flag.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            _ => false
        };
    }

    private static int ResolveEventsCount()
    {
        var env = Environment.GetEnvironmentVariable("OILERP__TESTS__LOAD_EVENTS");
        if (int.TryParse(env, out var n) && n > 0 && n <= 20000) return n;
        return 1000;
    }
}
