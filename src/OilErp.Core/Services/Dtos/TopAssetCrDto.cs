using System.Text.Json.Serialization;

namespace OilErp.Core.Services.Dtos;

public partial record TopAssetCrDto
{
    [JsonPropertyName("asset_code")] public string? AssetCode { get; init; }
    [JsonPropertyName("cr")] public decimal? Cr { get; init; }
    [JsonPropertyName("updated_at")] public DateTime? UpdatedAt { get; init; }
}

