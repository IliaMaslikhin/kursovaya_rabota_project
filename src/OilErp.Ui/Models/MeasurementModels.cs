using System;
using System.Collections.Generic;
using OilErp.Core.Dto;

namespace OilErp.Ui.Models;

public sealed class AddMeasurementRequest
{
    public string Plant { get; init; } = string.Empty;
    public string AssetCode { get; init; } = string.Empty;
    public MeasurementPointDto Measurement { get; init; } = default!;

    public AddMeasurementRequest() { }

    public AddMeasurementRequest(string plant, string assetCode, MeasurementPointDto measurement)
    {
        Plant = plant;
        AssetCode = assetCode;
        Measurement = measurement;
    }
}

public sealed class MeasurementSeries
{
    private readonly List<MeasurementPointDto> points;

    public string AssetCode { get; }
    public string SourcePlant { get; }
    public IReadOnlyList<MeasurementPointDto> Points => points;

    public MeasurementSeries() : this(string.Empty, string.Empty, Array.Empty<MeasurementPointDto>()) { }

    public MeasurementSeries(string assetCode, string sourcePlant, IEnumerable<MeasurementPointDto> points)
    {
        AssetCode = assetCode;
        SourcePlant = sourcePlant;
        this.points = new List<MeasurementPointDto>(points);
    }

    public void AddPoint(MeasurementPointDto point)
    {
        points.Add(point);
    }
}

public sealed record MeasurementSubmissionResult(bool Success, string Message, bool WrittenToDb);
