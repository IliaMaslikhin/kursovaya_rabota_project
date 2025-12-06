namespace OilErp.Core.Dto;

/// <summary>
/// Строка выдачи fn_eval_risk
/// </summary>
public sealed record EvalRiskRowDto(
    string? AssetCode,
    decimal? Cr,
    string? Level,
    decimal? ThresholdLow,
    decimal? ThresholdMed,
    decimal? ThresholdHigh);
