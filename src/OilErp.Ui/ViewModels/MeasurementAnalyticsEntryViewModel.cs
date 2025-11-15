using System;
using OilErp.Core.Dto;

namespace OilErp.Ui.ViewModels;

public sealed class MeasurementAnalyticsEntryViewModel
{
    public MeasurementAnalyticsEntryViewModel(string plant, string assetCode, MeasurementPointDto measurement)
    {
        Plant = plant;
        AssetCode = assetCode;
        Measurement = measurement;
    }

    public string Plant { get; }

    public string AssetCode { get; }

    public MeasurementPointDto Measurement { get; }

    public string Label => Measurement.Label;

    public string ThicknessDisplay => $"{Measurement.Thickness:F1} мм";

    public string TimestampDisplay => Measurement.Ts.ToLocalTime().ToString("dd MMM HH:mm");

    public string Note => string.IsNullOrWhiteSpace(Measurement.Note) ? "—" : Measurement.Note!;
}
