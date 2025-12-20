using System;

namespace OilErp.Core.Dto;

public sealed record EvalRiskRowDto(
    string AssetCode,
    decimal? Cr,
    string? Level,
    decimal? ThresholdLow,
    decimal? ThresholdMed,
    decimal? ThresholdHigh);

public sealed record PlantCrStatDto(decimal? CrMean, decimal? CrP90, int AssetsCount);

public sealed record MeasurementPointDto(string Label, DateTime Ts, decimal Thickness, string? Note = null);
