using System;
using System.Collections.Generic;
using System.Linq;
using OilErp.Core.Dto;

namespace OilErp.Ui.Models;

public sealed class MeasurementSeries
{
    public MeasurementSeries(string assetCode, string sourcePlant, IReadOnlyList<MeasurementPointDto> points)
    {
        AssetCode = assetCode;
        SourcePlant = sourcePlant;
        Points = points.OrderBy(p => p.Ts).ToList();
    }

    public string AssetCode { get; }

    public string SourcePlant { get; }

    public List<MeasurementPointDto> Points { get; }

    public MeasurementPointDto? LatestPoint => Points.LastOrDefault();

    public MeasurementPointDto? FirstPoint => Points.FirstOrDefault();

    public decimal Trend => (LatestPoint?.Thickness ?? 0m) - (FirstPoint?.Thickness ?? 0m);

    public void AddPoint(MeasurementPointDto point)
    {
        Points.Add(point);
        Points.Sort((a, b) => DateTime.Compare(a.Ts, b.Ts));
    }
}
