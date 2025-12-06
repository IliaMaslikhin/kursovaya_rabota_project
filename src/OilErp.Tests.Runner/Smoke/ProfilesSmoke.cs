using OilErp.Core.Dto;
using OilErp.Core.Operations;
using OilErp.Core.Services.Plants.ANPZ;
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
            var conn = Environment.GetEnvironmentVariable("OILERP__DB__CONN_ANPZ");
            if (string.IsNullOrWhiteSpace(conn))
            {
                return new TestResult(testName, true, "ANPZ conn not set, skipped", true);
            }

            var storageConfig = new StorageConfig(conn);
            var storage = new StorageAdapter(storageConfig);
            await using var tx = await storage.BeginTransactionAsync();

            // insert batch into plant proc
            var svc = new SpInsertMeasurementBatchService(storage);
            var json = "[{\"label\":\"HC\",\"ts\":\"2025-01-01T00:00:00Z\",\"thickness\":12.34}]";
            await svc.sp_insert_measurement_batchAsync("HC_CHECK_UI", json, "ANPZ", CancellationToken.None);

            // ensure FDW central inbox reachable
            await using (var connPlant = new NpgsqlConnection(storageConfig.ConnectionString))
            {
                await connPlant.OpenAsync();
                await using var cmd = connPlant.CreateCommand();
                cmd.CommandText = "select 1 from central_ft.events_inbox limit 1";
                await cmd.ExecuteScalarAsync();
            }

            await tx.RollbackAsync();
            return new TestResult(testName, true);
        }
        catch (Exception ex)
        {
            return new TestResult(testName, false, ex.Message);
        }
    }
}
