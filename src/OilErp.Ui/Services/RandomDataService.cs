using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NpgsqlTypes;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Services.Central;
using ANPZ = OilErp.Core.Services.Plants.ANPZ;
using KRNPZ = OilErp.Core.Services.Plants.KRNPZ;
using OilErp.Core.Util;

namespace OilErp.Ui.Services;

public sealed record RandomDataGenerationResult(int Assets, int Measurements, int Policies);

public sealed class RandomDataService
{
    private const int MinAssets = 15;
    private const int MaxAssets = 99;
    private const string CentralPlantCode = "CENTRAL";

    private static readonly string[] PlantStatusOptions = { "Норма", "Предупреждение", "Критично", "Неизвестно" };
    private static readonly string[] PlantLocations =
    {
        "Эстакада А",
        "Эстакада Б",
        "Установка 1",
        "Установка 2",
        "Парк резервуаров",
        "Зона налива",
        "Ремонтный цех",
        "Вне площадки"
    };

    private sealed record AssetKind(string Prefix, string Name, string Type, bool IsPipe);

    private static readonly AssetKind[] AssetKinds =
    {
        new("ТРУБА", "Труба", "ТРУБА", true),
        new("РЕЗЕРВУАР", "Резервуар", "РЕЗЕРВУАР", false),
        new("НАСОС", "Насос", "НАСОС", false),
        new("КЛАПАН", "Клапан", "КЛАПАН", false),
        new("ТЕПЛООБМЕННИК", "Теплообменник", "ТЕПЛООБМЕННИК", false)
    };

    private sealed record AssetSpec(string Code, string Name, string Type, bool IsPipe);

    private readonly IStoragePort storage;
    private readonly string connectionString;
    private readonly DatabaseProfile profile;
    private readonly Random random;

    public RandomDataService(IStoragePort storage, string connectionString, DatabaseProfile profile)
    {
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        this.profile = profile;
        random = Random.Shared;
    }

    public async Task<RandomDataGenerationResult> GenerateAsync(CancellationToken ct = default)
    {
        var assetCount = random.Next(MinAssets, MaxAssets + 1);
        return profile switch
        {
            DatabaseProfile.Central => await GenerateCentralAsync(assetCount, ct),
            DatabaseProfile.PlantAnpz or DatabaseProfile.PlantKrnpz => await GeneratePlantAsync(assetCount, ct),
            _ => throw new InvalidOperationException($"Unsupported profile {profile}")
        };
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = profile switch
        {
            DatabaseProfile.Central =>
                "truncate table public.measurement_batches, public.analytics_cr, public.risk_policies, public.assets_global restart identity cascade;",
            DatabaseProfile.PlantAnpz or DatabaseProfile.PlantKrnpz =>
                "truncate table public.measurements, public.measurement_points, public.assets_local, public.local_events restart identity cascade;",
            _ => throw new InvalidOperationException($"Unsupported profile {profile}")
        };

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<RandomDataGenerationResult> GenerateCentralAsync(int assetCount, CancellationToken ct)
    {
        var runId = BuildRunId();
        var assetService = new FnAssetUpsertService(storage);
        var assets = BuildAssets(runId, assetCount);

        foreach (var asset in assets)
        {
            await assetService.fn_asset_upsertAsync(asset.Code, asset.Name, asset.Type, CentralPlantCode, ct);
        }

        var policyCount = random.Next(2, 4);
        var policyService = new FnPolicyUpsertService(storage);
        for (var i = 1; i <= policyCount; i++)
        {
            var (low, med, high) = BuildPolicyThresholds();
            var name = $"Политика-{runId}-{i}";
            await policyService.fn_policy_upsertAsync(name, low, med, high, ct);
        }

        var pipeCodes = assets.Where(a => a.IsPipe).Select(a => a.Code).ToArray();
        var highCorrosionAssets = PickHighCorrosionAssets(pipeCodes);
        var measurementDates = BuildMeasurementDates();
        var measurementBatches = 0;
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        var hasExtras = await HasExtendedColumnsAsync(conn, ct);

        foreach (var asset in assets)
        {
            var points = BuildMeasurementSeries(measurementDates, PickPointCount(), highCorrosionAssets.Contains(asset.Code));
            decimal? prevThk = null;
            DateTime? prevDateUtc = null;

            foreach (var point in points)
            {
                await InsertCentralBatchAsync(conn, hasExtras, asset.Code, prevThk, prevDateUtc, point, ct);
                measurementBatches++;
                prevThk = point.Thickness;
                prevDateUtc = point.Ts;
            }
        }

        return new RandomDataGenerationResult(assetCount, measurementBatches, policyCount);
    }

    private async Task<RandomDataGenerationResult> GeneratePlantAsync(int assetCount, CancellationToken ct)
    {
        var runId = BuildRunId();
        var plantCode = profile == DatabaseProfile.PlantKrnpz ? "KNPZ" : "ANPZ";
        var assets = BuildAssets(runId, assetCount);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        var pipeCodes = assets.Where(a => a.IsPipe).Select(a => a.Code).ToArray();
        var highCorrosionAssets = PickHighCorrosionAssets(pipeCodes);
        var measurementDates = BuildMeasurementDates();
        foreach (var asset in assets)
        {
            var location = PlantLocations[random.Next(PlantLocations.Length)];
            var status = highCorrosionAssets.Contains(asset.Code)
                ? "Критично"
                : PlantStatusOptions[random.Next(PlantStatusOptions.Length)];
            await UpsertPlantAssetAsync(conn, asset.Code, location, status, ct);
        }

        var measurements = 0;
        if (profile == DatabaseProfile.PlantKrnpz)
        {
            var insertService = new KRNPZ.SpInsertMeasurementBatchService(storage);
            foreach (var asset in assets)
            {
                var points = BuildMeasurementSeries(measurementDates, PickPointCount(), highCorrosionAssets.Contains(asset.Code));
                var payload = MeasurementBatchPayloadBuilder.BuildJson(points);
                measurements += await insertService.sp_insert_measurement_batchAsync(asset.Code, payload, plantCode, ct);
            }
        }
        else
        {
            var insertService = new ANPZ.SpInsertMeasurementBatchService(storage);
            foreach (var asset in assets)
            {
                var points = BuildMeasurementSeries(measurementDates, PickPointCount(), highCorrosionAssets.Contains(asset.Code));
                var payload = MeasurementBatchPayloadBuilder.BuildJson(points);
                measurements += await insertService.sp_insert_measurement_batchAsync(asset.Code, payload, plantCode, ct);
            }
        }

        return new RandomDataGenerationResult(assetCount, measurements, 0);
    }

    private IReadOnlyList<MeasurementPointDto> BuildMeasurementSeries(
        IReadOnlyList<DateTime> datesUtc,
        int count,
        bool highCorrosion)
    {
        var normalizedCount = NormalizePointCount(count);
        var totalPoints = normalizedCount * datesUtc.Count;
        var points = new List<MeasurementPointDto>(totalPoints);

        var thickness = highCorrosion
            ? RoundDecimal((decimal)(random.NextDouble() * 4 + 10))
            : RoundDecimal((decimal)(random.NextDouble() * 6 + 6));

        for (var day = 0; day < datesUtc.Count; day++)
        {
            var dayStart = datesUtc[day].AddHours(8);
            var minutesStep = random.Next(30, 90);

            for (var i = 0; i < normalizedCount; i++)
            {
                var isLast = day == datesUtc.Count - 1 && i == normalizedCount - 1;
                if (i > 0 || day > 0)
                {
                    var drop = isLast && highCorrosion
                        ? (decimal)(random.NextDouble() * 5 + 5)
                        : (decimal)(random.NextDouble() * 0.007 + 0.001);
                    thickness = RoundDecimal(Math.Max(0.4m, thickness - drop));
                }

                var ts = dayStart.AddMinutes(i * minutesStep);
                var label = $"T{i + 1}";
                var note = isLast && highCorrosion
                    ? "высокая коррозия"
                    : random.NextDouble() < 0.15 ? "плановый осмотр" : null;
                points.Add(new MeasurementPointDto(label, DateTime.SpecifyKind(ts, DateTimeKind.Utc), thickness, note));
            }
        }

        return points;
    }

    private IReadOnlyList<DateTime> BuildMeasurementDates()
    {
        var dayCount = random.Next(2, 4);
        var dates = new List<DateTime>(dayCount);
        var currentDay = DateTime.UtcNow.Date.AddDays(-random.Next(60, 240));
        dates.Add(currentDay);

        for (var day = 1; day < dayCount; day++)
        {
            currentDay = currentDay.AddDays(random.Next(14, 45));
            dates.Add(currentDay);
        }

        return dates;
    }

    private (decimal Low, decimal Med, decimal High) BuildPolicyThresholds()
    {
        var low = RoundDecimal((decimal)(random.NextDouble() * 0.6 + 0.2));
        var med = RoundDecimal(low + (decimal)(random.NextDouble() * 0.6 + 0.2));
        var high = RoundDecimal(med + (decimal)(random.NextDouble() * 0.6 + 0.2));
        return (low, med, high);
    }

    private static decimal RoundDecimal(decimal value)
        => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    private int PickPointCount()
    {
        var options = new[] { 4, 6, 8, 10, 12 };
        return options[random.Next(options.Length)];
    }

    private static int NormalizePointCount(int value)
    {
        if (value < 4) return 4;
        if (value > 12) return 12;
        return value % 2 == 0 ? value : value + 1;
    }

    private HashSet<string> PickHighCorrosionAssets(IReadOnlyList<string> assetCodes)
    {
        var max = Math.Min(2, assetCodes.Count);
        if (max == 0) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var count = random.Next(1, max + 1);
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (selected.Count < count)
        {
            selected.Add(assetCodes[random.Next(assetCodes.Count)]);
        }

        return selected;
    }

    private List<AssetSpec> BuildAssets(string runId, int count)
    {
        var assets = new List<AssetSpec>(count);
        if (count <= 0) return assets;

        var nonPipeCount = Math.Min(4, Math.Max(2, count / 5));
        if (nonPipeCount > count - 2) nonPipeCount = Math.Max(1, count - 2);

        var nonPipeIndices = new HashSet<int>();
        while (nonPipeIndices.Count < nonPipeCount)
        {
            nonPipeIndices.Add(random.Next(count));
        }

        for (var i = 0; i < count; i++)
        {
            var kind = nonPipeIndices.Contains(i)
                ? PickNonPipeKind()
                : PickPreferredKind();
            var idx = i + 1;
            assets.Add(new AssetSpec(
                $"{kind.Prefix}-{runId}-{idx:000}",
                $"{kind.Name} {idx}",
                kind.Type,
                kind.IsPipe));
        }

        return assets;
    }

    private AssetKind PickPreferredKind()
    {
        return random.NextDouble() < 0.65
            ? AssetKinds[0]
            : AssetKinds[random.Next(AssetKinds.Length)];
    }

    private AssetKind PickNonPipeKind()
    {
        var idx = random.Next(1, AssetKinds.Length);
        return AssetKinds[idx];
    }

    private static async Task UpsertPlantAssetAsync(
        NpgsqlConnection conn,
        string code,
        string? location,
        string status,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            with upd as (
              update public.assets_local
              set location = @location,
                  status = @status
              where lower(asset_code) = lower(@code)
              returning 1
            )
            insert into public.assets_local(asset_code, location, status)
            select @code, @location, @status
            where not exists (select 1 from upd)
            """;
        cmd.Parameters.AddWithValue("@code", code.Trim());
        cmd.Parameters.AddWithValue("@location", (object?)location ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", status);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertCentralBatchAsync(
        NpgsqlConnection conn,
        bool hasExtras,
        string assetCode,
        decimal? prevThk,
        DateTime? prevDateUtc,
        MeasurementPointDto point,
        CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = hasExtras
            ? """
              insert into public.measurement_batches(
                source_plant, asset_code, prev_thk, prev_date, last_thk, last_date, last_label, last_note
              )
              values (@plant, @asset, @prev_thk, @prev_date, @last_thk, @last_date, @label, @note);
              """
            : """
              insert into public.measurement_batches(
                source_plant, asset_code, prev_thk, prev_date, last_thk, last_date
              )
              values (@plant, @asset, @prev_thk, @prev_date, @last_thk, @last_date);
              """;

        cmd.Parameters.Add("plant", NpgsqlDbType.Text).Value = CentralPlantCode;
        cmd.Parameters.Add("asset", NpgsqlDbType.Text).Value = assetCode.Trim();
        cmd.Parameters.Add("prev_thk", NpgsqlDbType.Numeric).Value = (object?)prevThk ?? DBNull.Value;
        cmd.Parameters.Add("prev_date", NpgsqlDbType.TimestampTz).Value = (object?)prevDateUtc ?? DBNull.Value;
        cmd.Parameters.Add("last_thk", NpgsqlDbType.Numeric).Value = point.Thickness;
        cmd.Parameters.Add("last_date", NpgsqlDbType.TimestampTz).Value = point.Ts;

        if (hasExtras)
        {
            cmd.Parameters.Add("label", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(point.Label) ? DBNull.Value : point.Label.Trim();
            cmd.Parameters.Add("note", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(point.Note) ? DBNull.Value : point.Note.Trim();
        }

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> HasExtendedColumnsAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          select 1
                          from information_schema.columns
                          where table_schema = 'public'
                            and table_name = 'measurement_batches'
                            and column_name in ('last_label','last_note')
                          limit 1
                          """;

        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    private string BuildRunId()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = new char[6];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[random.Next(alphabet.Length)];
        }

        var stamp = DateTime.UtcNow.ToString("yyMMddHHmm");
        return $"{stamp}{new string(chars)}";
    }
}
