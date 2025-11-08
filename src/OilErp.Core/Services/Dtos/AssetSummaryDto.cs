using System.Text.Json.Serialization;

namespace OilErp.Core.Services.Dtos;

// Пример JSON (central.fn_asset_summary_json):
// {
//   "asset": {"asset_code":"A1","name":"A1","type":null,"plant_code":"ANPZ"},
//   "analytics": {"prev_thk":10.1,"prev_date":"2025-01-01T00:00:00Z","last_thk":9.9,"last_date":"2025-01-31T00:00:00Z","cr":0.002,"updated_at":"2025-02-01T00:00:00Z"},
//   "risk": {"level":"LOW","threshold_low":0.001,"threshold_med":0.005,"threshold_high":0.01,"cr":0.002}
// }

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
