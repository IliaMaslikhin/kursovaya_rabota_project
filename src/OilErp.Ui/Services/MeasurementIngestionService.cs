using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OilErp.Bootstrap;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
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
    private readonly IStoragePort storage;

    public MeasurementIngestionService(IStoragePort storage)
    {
        this.storage = storage;
    }

    public async Task<MeasurementSubmissionResult> IngestAsync(
        AddMeasurementRequest request,
        DatabaseProfile? profile,
        CancellationToken ct)
    {
        var plant = NormalizePlant(request.Plant);
        var asset = request.AssetCode.Trim();
        var payload = BuildPointsJson(request.Measurement);

        try
        {
            AppLogger.Info($"[ui] ingest замера asset={asset} plant={plant}");
            var affected = await ExecutePlantCommandAsync(plant, asset, payload, ct);
            AppLogger.Info($"[ui] ingest завершен asset={asset} plant={plant} rows={affected}");
            return new MeasurementSubmissionResult(true, $"Записано в БД ({affected}) для {plant} · {asset}.", true);
        }
        catch (Exception ex)
        {
            var profileHint = profile.HasValue ? $" профиль {profile}" : " профиль неизвестен";
            var message = $"Не удалось сохранить в БД для {plant} ({profileHint}): {ex.Message}";
            AppLogger.Error($"[ui] ingest ошибка asset={asset} plant={plant}: {ex.Message}");
            return new MeasurementSubmissionResult(true, message, false);
        }
    }

    private async Task<int> ExecutePlantCommandAsync(string plant, string assetCode, string pointsJson, CancellationToken ct)
    {
        if (string.Equals(plant, "KRNPZ", StringComparison.OrdinalIgnoreCase))
        {
            var krnpz = new KrnpzInsertService(storage);
            return await krnpz.sp_insert_measurement_batchAsync(assetCode, pointsJson, plant, ct);
        }

        var anpz = new AnpzInsertService(storage);
        return await anpz.sp_insert_measurement_batchAsync(assetCode, pointsJson, plant, ct);
    }

    private static string BuildPointsJson(MeasurementPointDto dto)
    {
        var payload = new[]
        {
            new Dictionary<string, object?>
            {
                ["label"] = dto.Label,
                ["ts"] = dto.Ts.ToString("O"),
                ["thickness"] = dto.Thickness,
                ["note"] = dto.Note
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private static string NormalizePlant(string plant)
        => string.IsNullOrWhiteSpace(plant) ? "ANPZ" : plant.Trim().ToUpperInvariant();
}
