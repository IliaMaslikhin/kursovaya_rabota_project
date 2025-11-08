namespace OilErp.Core.Services.Dtos;

public sealed record PlantCrAggregateDto(
    string Plant,
    DateTime From,
    DateTime To,
    decimal? CrMean,
    decimal? CrP90,
    int AssetsConsidered
);

