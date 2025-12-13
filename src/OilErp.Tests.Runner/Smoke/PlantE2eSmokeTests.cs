using System;
using System.Threading.Tasks;
using Npgsql;
using OilErp.Core.Dto;
using OilErp.Core.Services.Central;
using OilErp.Core.Util;
using OilErp.Tests.Runner.Util;
using AnpzInsertService = OilErp.Core.Services.Plants.ANPZ.SpInsertMeasurementBatchService;
using KrnpzInsertService = OilErp.Core.Services.Plants.KRNPZ.SpInsertMeasurementBatchService;

namespace OilErp.Tests.Runner.Smoke;

/// <summary>
/// E2E-проверки: завод → central ingest → analytics_cr и очистка очереди.
/// </summary>
public class PlantE2eSmokeTests
{
    private readonly struct PlantProfile
    {
        public PlantProfile(DatabaseProfile profile, string envVar, string plantCode)
        {
            Profile = profile;
            EnvVar = envVar;
            PlantCode = plantCode;
        }

        public DatabaseProfile Profile { get; }
        public string EnvVar { get; }
        public string PlantCode { get; }
    }

    private static readonly PlantProfile[] PlantProfiles =
    {
        new(DatabaseProfile.PlantAnpz, "OILERP__DB__CONN_ANPZ", "ANPZ"),
        new(DatabaseProfile.PlantKrnpz, "OILERP__DB__CONN_KRNPZ", "KRNPZ")
    };

    /// <summary>
    /// Проверяет, что события с заводов обновляют analytics_cr и не остаются в очереди.
    /// </summary>
    public async Task<TestResult> TestPlantEventsReachAnalytics()
    {
        const string testName = "Plant_Events_Reach_Analytics";
        var centralConfig = TestEnvironment.LoadStorageConfig(DatabaseProfile.Central);
        var centralStorage = TestEnvironment.CreateStorageAdapter(DatabaseProfile.Central);

        var processedPlants = 0;

        foreach (var plant in PlantProfiles)
        {
            var plantConn = Environment.GetEnvironmentVariable(plant.EnvVar);
            if (string.IsNullOrWhiteSpace(plantConn)) continue;

            processedPlants++;
            var assetCode = $"E2E_{plant.PlantCode}_{Guid.NewGuid():N}".Substring(0, 24);
            var payload = MeasurementBatchPayloadBuilder.BuildJson(
                new MeasurementPointDto("CP-E2E", DateTime.UtcNow.AddMinutes(-15), 12.34m));

            // 1) отправляем батч на завод
            try
            {
                var plantStorage = TestEnvironment.CreateStorageAdapter(plant.Profile);
                if (string.Equals(plant.PlantCode, "KRNPZ", StringComparison.OrdinalIgnoreCase))
                {
                    var svc = new KrnpzInsertService(plantStorage);
                    await svc.sp_insert_measurement_batchAsync(assetCode, payload, plant.PlantCode, CancellationToken.None);
                }
                else
                {
                    var svc = new AnpzInsertService(plantStorage);
                    await svc.sp_insert_measurement_batchAsync(assetCode, payload, plant.PlantCode, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                return new TestResult(testName, false, $"Завод {plant.PlantCode}: вставка не прошла ({ex.Message})");
            }

            // 2) убеждаемся, что событие попало в inbox
            var eventExists = await HasInboxEventAsync(centralConfig.ConnectionString, assetCode);
            if (!eventExists)
            {
                await CleanupCentralAsync(centralConfig.ConnectionString, assetCode);
                await CleanupPlantAsync(plant, assetCode);
                return new TestResult(testName, false, $"Событие не попало в inbox для {plant.PlantCode}");
            }

            // 3) ingest и проверки
            try
            {
                var ingest = new FnIngestEventsService(centralStorage);
                await ingest.fn_ingest_eventsAsync(100, CancellationToken.None);

                var analyticsUpdated = await HasAnalyticsAsync(centralConfig.ConnectionString, assetCode);
                if (!analyticsUpdated)
                {
                    await CleanupCentralAsync(centralConfig.ConnectionString, assetCode);
                    await CleanupPlantAsync(plant, assetCode);
                    return new TestResult(testName, false, $"analytics_cr не обновилась для {assetCode}");
                }

                var queueClean = await IsQueueCleanAsync(centralConfig.ConnectionString, assetCode);
                if (!queueClean)
                {
                    await CleanupCentralAsync(centralConfig.ConnectionString, assetCode);
                    await CleanupPlantAsync(plant, assetCode);
                    return new TestResult(testName, false, $"Очередь не очищена для {assetCode}");
                }
            }
            catch (Exception ex)
            {
                await CleanupCentralAsync(centralConfig.ConnectionString, assetCode);
                await CleanupPlantAsync(plant, assetCode);
                return new TestResult(testName, false, $"Ошибка ingest для {plant.PlantCode}: {ex.Message}");
            }

            await CleanupCentralAsync(centralConfig.ConnectionString, assetCode);
            await CleanupPlantAsync(plant, assetCode);
        }

        if (processedPlants == 0)
        {
            return new TestResult(testName, true, "Plant profiles not configured; skipped", true);
        }

        return new TestResult(testName, true);
    }

    private static async Task<bool> HasInboxEventAsync(string connString, string assetCode)
    {
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"select 1 from public.events_inbox where payload_json->>'asset_code' = @asset limit 1";
        cmd.Parameters.AddWithValue("@asset", assetCode);
        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }

    private static async Task<bool> HasAnalyticsAsync(string connString, string assetCode)
    {
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"select last_thk, last_date from public.analytics_cr where asset_code = @asset";
        cmd.Parameters.AddWithValue("@asset", assetCode);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return false;
        var lastThk = reader.IsDBNull(0) ? (decimal?)null : reader.GetDecimal(0);
        var lastDate = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
        return lastThk.HasValue && lastDate.HasValue;
    }

    private static async Task<bool> IsQueueCleanAsync(string connString, string assetCode)
    {
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"select count(*) from public.events_inbox where payload_json->>'asset_code' = @asset and processed_at is null";
        cmd.Parameters.AddWithValue("@asset", assetCode);
        var count = (long)await cmd.ExecuteScalarAsync();
        return count == 0;
    }

    private static async Task CleanupCentralAsync(string connString, string assetCode)
    {
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "delete from public.events_inbox where payload_json->>'asset_code' = @asset";
            cmd.Parameters.AddWithValue("@asset", assetCode);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "delete from public.analytics_cr where asset_code = @asset";
            cmd.Parameters.AddWithValue("@asset", assetCode);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "delete from public.assets_global where asset_code = @asset";
            cmd.Parameters.AddWithValue("@asset", assetCode);
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }

    private static async Task CleanupPlantAsync(PlantProfile plant, string assetCode)
    {
        var cfg = TestEnvironment.LoadStorageConfig(plant.Profile);
        await using var conn = new NpgsqlConnection(cfg.ConnectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
delete from public.measurements m
using public.measurement_points mp, public.assets_local a
where m.point_id = mp.id and mp.asset_id = a.id and a.asset_code = @asset;
delete from public.measurement_points mp using public.assets_local a
where mp.asset_id = a.id and a.asset_code = @asset;
delete from public.assets_local where asset_code = @asset;";
            cmd.Parameters.AddWithValue("@asset", assetCode);
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }
}
