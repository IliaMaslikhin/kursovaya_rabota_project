namespace OilErp.Core.Dto;

/// <summary>
/// Строка fn_plant_cr_stats
/// </summary>
public sealed record PlantCrStatDto(
    decimal? CrMean,
    decimal? CrP90,
    int AssetsCount);
