using OilErp.Core.Dto;
using OilErp.Core.Util;
using OilErp.Core.Services.Plants.ANPZ;
using OilErp.Infrastructure.Adapters;
using OilErp.Tests.Runner.Util;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// Негативные/валидирующие сценарии.
/// </summary>
public class NegativeSmokeTests
{
    /// <summary>
    /// Проверяет, что пустой asset_code в заводской функции приводит к ошибке.
    /// </summary>
    public async Task<TestResult> TestPlantInsertValidationFails()
    {
        const string testName = "Plant_Insert_Validation_Fails";
        try
        {
            var storage = TestEnvironment.CreateStorageAdapter(DatabaseProfile.PlantAnpz);
            var svc = new SpInsertMeasurementBatchService(storage);
            var payload = MeasurementBatchPayloadBuilder.BuildJson(
                new MeasurementPointDto("A", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), 12.3m));
            await svc.sp_insert_measurement_batchAsync("", payload, "ANPZ", CancellationToken.None);
            return new TestResult(testName, false, "ожидалось исключение при пустом asset_code");
        }
        catch
        {
            return new TestResult(testName, true);
        }
    }

    /// <summary>
    /// Проверяет, что невалидный payload для fn_events_enqueue приводит к ошибке парсинга jsonb.
    /// </summary>
    public async Task<TestResult> TestEventsEnqueueInvalidJson()
    {
        const string testName = "Events_Enqueue_Invalid_Json";
        try
        {
            var storage = TestEnvironment.CreateStorageAdapter();
            var spec = new CommandSpec(
                "public.fn_events_enqueue",
                new Dictionary<string, object?>
                {
                    ["p_event_type"] = "HC_MEASUREMENT_BATCH",
                    ["p_source_plant"] = "ANPZ",
                    ["p_payload"] = "not-a-json"
                });
            await storage.ExecuteCommandAsync(spec);
            return new TestResult(testName, false, "ожидалось исключение при невалидном JSON");
        }
        catch
        {
            return new TestResult(testName, true);
        }
    }
}
