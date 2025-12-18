using System;
using System.Threading;
using System.Threading.Tasks;
using OilErp.Bootstrap;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Util;
using AnpzInsertService = OilErp.Core.Services.Plants.ANPZ.SpInsertMeasurementBatchService;
using KrnpzInsertService = OilErp.Core.Services.Plants.KRNPZ.SpInsertMeasurementBatchService;
using OilErp.Ui.Models;

namespace OilErp.Ui.Services;

/// <summary>
/// Coordinates measurement ingestion into plant procedures using existing Core services.
/// Falls back to local-only status when the storage port is offline.
/// </summary>
public sealed class MeasurementIngestionService
{
    private readonly StoragePortFactory factory;

    public MeasurementIngestionService(StoragePortFactory factory)
    {
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<MeasurementSubmissionResult> IngestAsync(
        AddMeasurementRequest request,
        CancellationToken ct)
    {
        var plant = NormalizePlant(request.Plant);
        var asset = request.AssetCode.Trim();
        var payload = MeasurementBatchPayloadBuilder.BuildJson(request.Measurement);

        try
        {
            AppLogger.Info($"[ui] ingest замера asset={asset} plant={plant}");
            var storage = factory.ForPlant(plant);
            await using var tx = await storage.BeginTransactionAsync(ct);
            var affected = await ExecutePlantCommandAsync(storage, plant, asset, payload, ct);
            await tx.CommitAsync(ct);
            AppLogger.Info($"[ui] ingest завершен asset={asset} plant={plant} rows={affected}");
            return new MeasurementSubmissionResult(true, $"Записано в БД ({affected}) для {plant} · {asset}.", true);
        }
        catch (Exception ex)
        {
            var message = $"Не удалось сохранить в БД для {plant}: {ex.Message}";
            AppLogger.Error($"[ui] ingest ошибка asset={asset} plant={plant}: {ex.Message}");
            return new MeasurementSubmissionResult(false, message, false);
        }
    }

    private async Task<int> ExecutePlantCommandAsync(IStoragePort storage, string plant, string assetCode, string pointsJson, CancellationToken ct)
    {
        if (string.Equals(plant, "KRNPZ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(plant, "KNPZ", StringComparison.OrdinalIgnoreCase))
        {
            var krnpz = new KrnpzInsertService(storage);
            return await krnpz.sp_insert_measurement_batchAsync(assetCode, pointsJson, plant, ct);
        }

        var anpz = new AnpzInsertService(storage);
        return await anpz.sp_insert_measurement_batchAsync(assetCode, pointsJson, plant, ct);
    }

    private static string NormalizePlant(string plant)
    {
        if (string.IsNullOrWhiteSpace(plant)) return "ANPZ";

        var value = plant.Trim().ToUpperInvariant();
        return value == "KRNPZ" ? "KNPZ" : value;
    }
}
