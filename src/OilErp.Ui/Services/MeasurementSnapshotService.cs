using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using OilErp.Core.Dto;
using OilErp.Ui.Models;

namespace OilErp.Ui.Services;

public sealed class MeasurementSnapshotService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string dataDirectory;

    public MeasurementSnapshotService(string dataDirectory)
    {
        this.dataDirectory = dataDirectory;
    }

    public static MeasurementSnapshotService CreateDefault()
    {
        var baseDir = Path.Combine(AppContext.BaseDirectory, "Data");
        return new MeasurementSnapshotService(baseDir);
    }

    public IReadOnlyList<MeasurementSeries> LoadSeries()
    {
        if (!Directory.Exists(dataDirectory))
        {
            return Array.Empty<MeasurementSeries>();
        }

        var result = new List<MeasurementSeries>();
        foreach (var file in Directory.EnumerateFiles(dataDirectory, "*_measurements.json"))
        {
            using var stream = File.OpenRead(file);
            var snapshot = JsonSerializer.Deserialize<MeasurementFile>(stream, SerializerOptions);
            if (snapshot is null)
            {
                continue;
            }

            var points = snapshot.Points?
                             .Select(p => new MeasurementPointDto(
                                 p.Label,
                                 ParseTimestamp(p.Ts),
                                 (decimal)Math.Round(p.Thickness, 2),
                                 p.Note))
                             .ToList()
                         ?? new List<MeasurementPointDto>();

            result.Add(new MeasurementSeries(snapshot.AssetCode, snapshot.SourcePlant, points));
        }

        return result;
    }

    private static DateTime ParseTimestamp(string value)
    {
        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return parsed;
        }

        return DateTime.UtcNow;
    }

    private sealed record MeasurementFile(
        string AssetCode,
        string SourcePlant,
        IReadOnlyList<MeasurementPoint> Points);

    private sealed record MeasurementPoint(
        string Label,
        string Ts,
        double Thickness,
        string? Note);
}
