using OilErp.Bootstrap;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Services.Plants.ANPZ;
using OilErp.Core.Util;
using OilErp.Tests.Runner;
using OilErp.Tests.Runner.TestDoubles;
using OilErp.Tests.Runner.Util;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>Асинхронность/устойчивость, негативные проверки и проверка маркера bootstrap.</summary>
public class CommonSmokeTests
{
    /// <summary>Фейковое хранилище: счётчик параллельных команд.</summary>
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
            return calls == 10
                ? new TestResult(testName, true)
                : new TestResult(testName, false, $"Expected 10 calls, got {calls}");
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }

    /// <summary>ANPZ: пустой asset_code должен падать.</summary>
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

    /// <summary>Проверка маркера первого запуска/кода машины.</summary>
    public Task<TestResult> TestMachineCodeMarkerPersisted()
    {
        const string testName = "Machine_Code_Marker";
        try
        {
            var initialFirstRun = FirstRunTracker.IsFirstRun(out var codeBefore);
            FirstRunTracker.MarkCompleted(codeBefore);
            var afterMarkFirstRun = FirstRunTracker.IsFirstRun(out var codeAfter);

            if (afterMarkFirstRun)
            {
                return Task.FromResult(new TestResult(testName, false, "Маркер первого запуска не записан", false));
            }

            if (!string.Equals(codeBefore, codeAfter, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new TestResult(testName, false, $"Код машины изменился: {codeBefore} -> {codeAfter}", false));
            }

            return Task.FromResult(new TestResult(testName, true, initialFirstRun ? "Первый запуск зафиксирован" : null, false));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new TestResult(testName, false, ex.Message, false));
        }
    }
}
