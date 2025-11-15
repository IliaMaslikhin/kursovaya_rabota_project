using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Services.Central;
using OilErp.Ui.Models;

namespace OilErp.Ui.Services;

public sealed class MeasurementDataProvider
{
    private static readonly TimeSpan LiveLoadTimeout = TimeSpan.FromSeconds(3);

    private readonly IStoragePort storage;
    private readonly MeasurementSnapshotService fallbackSnapshots;

    public MeasurementDataProvider(IStoragePort storage, MeasurementSnapshotService fallbackSnapshots)
    {
        this.storage = storage;
        this.fallbackSnapshots = fallbackSnapshots;
    }

    public MeasurementDataResult Load()
    {
        if (storage is InMemoryStoragePort)
        {
            return new MeasurementDataResult(fallbackSnapshots.LoadSeries(), "Резервные JSON-данные (ядро офлайн).");
        }

        try
        {
            using var cts = new CancellationTokenSource(LiveLoadTimeout);
            var series = LoadFromKernel(cts.Token);
            if (series.Count > 0)
            {
                return new MeasurementDataResult(series, "Данные из центральной БД (StorageAdapter).");
            }

            return new MeasurementDataResult(fallbackSnapshots.LoadSeries(), "Резервные JSON-данные (центральная БД вернула пустой набор).");
        }
        catch (Exception ex)
        {
            return new MeasurementDataResult(fallbackSnapshots.LoadSeries(), $"Резервные JSON-данные (ошибка БД: {ex.Message})");
        }
    }

    private IReadOnlyList<MeasurementSeries> LoadFromKernel(CancellationToken ct)
    {
        var topService = new FnTopAssetsByCrService(storage);
        var summaryService = new FnAssetSummaryJsonService(storage);

        var topAssets = topService.fn_top_assets_by_crAsync(12, ct).GetAwaiter().GetResult();
        var result = new List<MeasurementSeries>();
        foreach (var row in topAssets)
        {
            ct.ThrowIfCancellationRequested();

            var assetCode = ReadString(row, "asset_code", "asset");
            if (string.IsNullOrWhiteSpace(assetCode))
            {
                continue;
            }

            var plant = ReadString(row, "plant_code", "plant") ?? "CENTRAL";
            var summary = summaryService.fn_asset_summary_jsonAsync(assetCode, "default", ct).GetAwaiter().GetResult();
            var points = BuildPointsFromSummary(assetCode, summary, row);
            if (points.Count == 0)
            {
                continue;
            }

            result.Add(new MeasurementSeries(assetCode, plant, points));
        }

        return result;
    }

    private static List<MeasurementPointDto> BuildPointsFromSummary(
        string assetCode,
        Core.Services.Dtos.AssetSummaryDto? summary,
        IReadOnlyDictionary<string, object?> rowFallback)
    {
        var points = new List<MeasurementPointDto>();
        var analytics = summary?.Analytics;
        if (analytics?.PrevThk is { } prevThk && analytics.PrevDate is { } prevDate)
        {
            points.Add(new MeasurementPointDto($"{assetCode}-prev", prevDate, prevThk, "Предыдущий замер из БД"));
        }

        if (analytics?.LastThk is { } lastThk && analytics.LastDate is { } latestDate)
        {
            points.Add(new MeasurementPointDto($"{assetCode}-last", latestDate, lastThk, "Текущий замер"));
        }

        if (points.Count == 0)
        {
            var lastThickness = ReadDecimal(rowFallback, "last_thk");
            var fallbackDate = ReadDate(rowFallback, "last_date");
            if (lastThickness is not null && fallbackDate is not null)
            {
                points.Add(new MeasurementPointDto($"{assetCode}-last", fallbackDate.Value, lastThickness.Value, "Данные top_assets_by_cr"));
            }
        }

        return points;
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetValue(name, out var value) && value is not null)
            {
                return value.ToString();
            }

            var kvp = row.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value is not null)
            {
                return kvp.Value.ToString();
            }
        }
        return null;
    }

    private static decimal? ReadDecimal(IReadOnlyDictionary<string, object?> row, params string[] names)
    {
        var value = ReadString(row, names);
        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }
        return null;
    }

    private static DateTime? ReadDate(IReadOnlyDictionary<string, object?> row, params string[] names)
    {
        var value = ReadString(row, names);
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result))
        {
            return result;
        }
        return null;
    }
}

public sealed record MeasurementDataResult(IReadOnlyList<MeasurementSeries> Series, string StatusMessage);
