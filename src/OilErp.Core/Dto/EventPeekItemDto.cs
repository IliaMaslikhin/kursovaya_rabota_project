namespace OilErp.Core.Dto;

/// <summary>
/// Строка fn_events_peek
/// </summary>
public sealed record EventPeekItemDto(
    long Id,
    string? EventType,
    string? SourcePlant,
    string PayloadJson,
    DateTime CreatedAt);
