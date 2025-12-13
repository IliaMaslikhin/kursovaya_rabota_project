using OilErp.Core.Dto;
using OilErp.Core.Operations;
using OilErp.Core.Services.Plants.ANPZ;
using OilErp.Core.Util;
using OilErp.Infrastructure.Adapters;
using OilErp.Infrastructure.Config;
using OilErp.Tests.Runner.Util;
using Npgsql;
using OilErp.Bootstrap;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// Проверки готовности профилей БД и базовых операций с откатом.
/// </summary>
public class ProfilesSmoke
{
    /// <summary>
    /// Проверяет наличие обязательных объектов во всех профилях, которые заданы в окружении.
    /// </summary>
    public async Task<TestResult> TestAllProfilesInventory()
    {
        const string testName = "Profiles_Inventory_All";
        try
        {
            var profiles = new[]
            {
                (Profile: DatabaseProfile.Central, EnvVar: "OILERP__DB__CONN"),
                (Profile: DatabaseProfile.PlantAnpz, EnvVar: "OILERP__DB__CONN_ANPZ"),
                (Profile: DatabaseProfile.PlantKrnpz, EnvVar: "OILERP__DB__CONN_KRNPZ")
            };

            foreach (var (profile, env) in profiles)
            {
                var conn = Environment.GetEnvironmentVariable(env);
                if (string.IsNullOrWhiteSpace(conn)) continue;

                var inspector = new DatabaseInventoryInspector(conn);
                var verification = await inspector.VerifyAsync();
                if (!verification.Success)
                {
                    return new TestResult(testName, false, $"Профиль {profile}: {verification.ErrorMessage}");
                }
            }

            return new TestResult(testName, true);
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }

    /// <summary>
    /// Делает тестовый вызов заводской процедуры с откатом и проверяет доступность FDW таблицы central_ft.events_inbox.
    /// </summary>
    public async Task<TestResult> TestPlantInsertAndFdwRoundtrip()
    {
        const string testName = "Plant_Insert_And_FDW_Roundtrip";
        try
        {
            var plants = new[]
            {
                (Profile: DatabaseProfile.PlantAnpz, Env: "OILERP__DB__CONN_ANPZ", DefaultPlant: "ANPZ"),
                (Profile: DatabaseProfile.PlantKrnpz, Env: "OILERP__DB__CONN_KRNPZ", DefaultPlant: "KRNPZ")
            };

            foreach (var plant in plants)
            {
                var conn = Environment.GetEnvironmentVariable(plant.Env);
                if (string.IsNullOrWhiteSpace(conn)) continue;

                var storageConfig = new StorageConfig(conn);
                var storage = new StorageAdapter(storageConfig);
                await using var tx = await storage.BeginTransactionAsync();

                var svcFn = new SpInsertMeasurementBatchService(storage);
                var svcPrc = new SpInsertMeasurementBatchPrcService(storage);
                var json = MeasurementBatchPayloadBuilder.BuildJson(
                    new MeasurementPointDto("HC", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), 12.34m));
                await svcFn.sp_insert_measurement_batchAsync($"HC_FN_{plant.DefaultPlant}", json, plant.DefaultPlant, CancellationToken.None);
                await svcPrc.sp_insert_measurement_batch_prcAsync($"HC_PRC_{plant.DefaultPlant}", json, plant.DefaultPlant, CancellationToken.None);

                // ensure FDW central inbox reachable
                await using (var connPlant = new NpgsqlConnection(storageConfig.ConnectionString))
                {
                    await connPlant.OpenAsync();
                    await using var cmd = connPlant.CreateCommand();
                    cmd.CommandText = "select 1 from central_ft.events_inbox limit 1";
                    await cmd.ExecuteScalarAsync();
                }

                await tx.RollbackAsync();
            }

            return new TestResult(testName, true);
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }
}
