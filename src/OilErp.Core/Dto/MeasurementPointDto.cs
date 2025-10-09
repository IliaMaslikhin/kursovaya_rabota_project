namespace OilErp.Core.Dto;

/// <summary>
/// Measurement point data transfer object
/// </summary>
/// <param name="Label">Measurement point label</param>
/// <param name="Ts">Timestamp of the measurement</param>
/// <param name="Thickness">Thickness value</param>
/// <param name="Note">Optional note</param>
public record MeasurementPointDto(
    string Label,
    DateTime Ts,
    decimal Thickness,
    string? Note = null
);
