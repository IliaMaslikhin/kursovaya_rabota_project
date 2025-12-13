using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using OilErp.Core.Dto;

namespace OilErp.Core.Util;

/// <summary>
/// Builds canonical JSON payloads for measurement batches accepted by plant procedures.
/// Keeps label/ts/thickness/note keys aligned across UI/CLI/Tests.
/// </summary>
public static class MeasurementBatchPayloadBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = false
    };

    /// <summary>
    /// Build JSON array payload from measurement points.
    /// </summary>
    /// <exception cref="ArgumentNullException">points is null</exception>
    /// <exception cref="ArgumentException">points is empty or contains invalid values</exception>
    public static string BuildJson(IEnumerable<MeasurementPointDto> points, bool sortByTimestamp = true)
    {
        if (points is null) throw new ArgumentNullException(nameof(points));

        var normalized = points.Select(NormalizePoint).ToList();
        if (normalized.Count == 0)
            throw new ArgumentException("At least one measurement point is required", nameof(points));

        var ordered = sortByTimestamp
            ? normalized.OrderBy(p => p.Ts)
            : normalized.AsEnumerable();

        var payload = ordered.Select(p => new Dictionary<string, object?>
        {
            ["label"] = p.Label,
            ["ts"] = p.Ts.ToString("O"),
            ["thickness"] = p.Thickness,
            ["note"] = p.Note
        });

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    /// <summary>
    /// Convenience for a single point.
    /// </summary>
    public static string BuildJson(MeasurementPointDto point) => BuildJson(new[] { point });

    private static MeasurementPointDto NormalizePoint(MeasurementPointDto point)
    {
        if (string.IsNullOrWhiteSpace(point.Label))
            throw new ArgumentException("Label is required", nameof(point));

        var label = point.Label.Trim();
        var ts = NormalizeTimestamp(point.Ts);
        var note = string.IsNullOrWhiteSpace(point.Note) ? null : point.Note.Trim();

        return point with { Label = label, Ts = ts, Note = note };
    }

    private static DateTime NormalizeTimestamp(DateTime ts)
    {
        return ts.Kind switch
        {
            DateTimeKind.Unspecified => DateTime.SpecifyKind(ts, DateTimeKind.Utc),
            DateTimeKind.Local => ts.ToUniversalTime(),
            _ => ts
        };
    }
}
