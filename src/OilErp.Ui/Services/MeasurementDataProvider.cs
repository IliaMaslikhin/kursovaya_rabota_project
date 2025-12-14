using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OilErp.Bootstrap;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Services.Central;
using OilErp.Core.Services.Dtos;
using OilErp.Ui.Models;

namespace OilErp.Ui.Services;

public sealed class MeasurementDataProvider
{
    private static readonly TimeSpan LiveLoadTimeout = TimeSpan.FromSeconds(10);

    private readonly IStoragePort storage;

    public MeasurementDataProvider(IStoragePort storage)
    {
        this.storage = storage;
    }

    public async Task<MeasurementDataResult> LoadAsync(CancellationToken ct = default)
    {
        AppLogger.Info("[ui] загрузка данных для UI");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(LiveLoadTimeout);
        var series = await LoadFromKernelAsync(cts.Token);
        if (series.Count > 0)
        {
            AppLogger.Info($"[ui] получено {series.Count} рядов из БД");
            return new MeasurementDataResult(series, "Данные из центральной БД (StorageAdapter).");
        }

        AppLogger.Info("[ui] БД вернула пустой набор измерений");
        return new MeasurementDataResult(Array.Empty<MeasurementSeries>(), "БД пуста: измерений пока нет.");
    }

    private async Task<IReadOnlyList<MeasurementSeries>> LoadFromKernelAsync(CancellationToken ct)
    {
        var topService = new FnTopAssetsByCrService(storage);
        var summaryService = new FnAssetSummaryJsonService(storage);

        var topAssets = await topService.fn_top_assets_by_crAsync(12, ct);
        var result = new List<MeasurementSeries>();
        foreach (var row in topAssets)
        {
            ct.ThrowIfCancellationRequested();
            var assetCode = row.AssetCode;
            if (string.IsNullOrWhiteSpace(assetCode))
            {
                continue;
            }

            var plant = "CENTRAL";
            var summary = await summaryService.fn_asset_summary_jsonAsync(assetCode, "default", ct);
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
        TopAssetCrDto rowFallback)
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
            var lastThickness = rowFallback.Cr;
            var fallbackDate = rowFallback.UpdatedAt;
            if (lastThickness is not null && fallbackDate is not null)
            {
                points.Add(new MeasurementPointDto($"{assetCode}-last", fallbackDate.Value, lastThickness.Value, "Данные top_assets_by_cr"));
            }
        }

        return points;
    }

}

public sealed record MeasurementDataResult(IReadOnlyList<MeasurementSeries> Series, string StatusMessage);
