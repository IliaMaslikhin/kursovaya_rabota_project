using System;
using System.Text.Json.Serialization;

namespace OilErp.Core.Dto;

public sealed record PlantCrAggregateDto(
    string Plant,
    DateTime From,
    DateTime To,
    decimal? CrMean,
    decimal? CrP90,
    int AssetsConsidered);

public sealed record AssetSummaryDto
{
    [JsonPropertyName("asset")] public AssetInfo Asset { get; init; } = new();
    [JsonPropertyName("analytics")] public AnalyticsInfo? Analytics { get; init; }
    [JsonPropertyName("risk")] public RiskInfo? Risk { get; init; }

    public sealed record AssetInfo
    {
        [JsonPropertyName("asset_code")] public string? AssetCode { get; init; }
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("type")] public string? Type { get; init; }
        [JsonPropertyName("plant_code")] public string? PlantCode { get; init; }
    }

    public sealed record AnalyticsInfo
    {
        [JsonPropertyName("prev_thk")] public decimal? PrevThk { get; init; }
        [JsonPropertyName("prev_date")] public DateTime? PrevDate { get; init; }
        [JsonPropertyName("last_thk")] public decimal? LastThk { get; init; }
        [JsonPropertyName("last_date")] public DateTime? LastDate { get; init; }
        [JsonPropertyName("cr")] public decimal? Cr { get; init; }
        [JsonPropertyName("updated_at")] public DateTime? UpdatedAt { get; init; }
    }

    public sealed record RiskInfo
    {
        [JsonPropertyName("level")] public string? Level { get; init; }
        [JsonPropertyName("threshold_low")] public decimal? ThresholdLow { get; init; }
        [JsonPropertyName("threshold_med")] public decimal? ThresholdMed { get; init; }
        [JsonPropertyName("threshold_high")] public decimal? ThresholdHigh { get; init; }
        [JsonPropertyName("cr")] public decimal? Cr { get; init; }
    }
}

public sealed record TopAssetCrDto
{
    [JsonPropertyName("asset_code")] public string? AssetCode { get; init; }
    [JsonPropertyName("cr")] public decimal? Cr { get; init; }
    [JsonPropertyName("updated_at")] public DateTime? UpdatedAt { get; init; }
}
