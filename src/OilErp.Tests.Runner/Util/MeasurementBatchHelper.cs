using System.Globalization;
using System.Text;
using System.Text.Json;
using OilErp.Core.Dto;
using OilErp.Core.Util;

namespace OilErp.Tests.Runner.Util;

internal static class MeasurementBatchHelper
{
    public static (string AssetCode, string SourcePlant, string PointsJson) ParseFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("batch file not found", path);
        var ext = Path.GetExtension(path);
        return ext.Equals(".json", StringComparison.OrdinalIgnoreCase)
            ? ParseJson(path)
            : ParseCsv(path);
    }

    private static (string AssetCode, string SourcePlant, string PointsJson) ParseJson(string path)
    {
        using var fs = File.OpenRead(path);
        using var doc = JsonDocument.Parse(fs);
        var root = doc.RootElement;
        var asset = root.GetProperty("asset_code").GetString() ?? throw new InvalidOperationException("asset_code required");
        var source = root.TryGetProperty("source_plant", out var sp) && sp.ValueKind == JsonValueKind.String
            ? sp.GetString() ?? "ANPZ"
            : "ANPZ";

        if (!root.TryGetProperty("points", out var pointsElement) || pointsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("points array is required");

        var points = pointsElement.EnumerateArray().Select(ReadPoint).ToList();
        var pointsJson = MeasurementBatchPayloadBuilder.BuildJson(points);
        return (asset, source, pointsJson);
    }

    private static (string AssetCode, string SourcePlant, string PointsJson) ParseCsv(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2) throw new InvalidOperationException("CSV has no data");
        var header = lines[0].Split(',').Select(s => s.Trim()).ToArray();
        int idxAsset = Array.FindIndex(header, h => string.Equals(h, "asset_code", StringComparison.OrdinalIgnoreCase));
        int idxLabel = Array.FindIndex(header, h => string.Equals(h, "label", StringComparison.OrdinalIgnoreCase));
        int idxTs = Array.FindIndex(header, h => string.Equals(h, "ts", StringComparison.OrdinalIgnoreCase));
        int idxThk = Array.FindIndex(header, h => string.Equals(h, "thickness", StringComparison.OrdinalIgnoreCase));
        int idxNote = Array.FindIndex(header, h => string.Equals(h, "note", StringComparison.OrdinalIgnoreCase));
        int idxPlant = Array.FindIndex(header, h => string.Equals(h, "source_plant", StringComparison.OrdinalIgnoreCase));
        if (idxAsset < 0 || idxLabel < 0 || idxTs < 0 || idxThk < 0)
            throw new InvalidOperationException("CSV header must include asset_code,label,ts,thickness");

        var points = new List<MeasurementPointDto>();
        string assetCode = string.Empty;
        string sourcePlant = "ANPZ";
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = SplitCsv(line);
            var ac = cols.ElementAtOrDefault(idxAsset) ?? string.Empty;
            var lbl = cols.ElementAtOrDefault(idxLabel) ?? string.Empty;
            var tsStr = cols.ElementAtOrDefault(idxTs) ?? string.Empty;
            var thkStr = cols.ElementAtOrDefault(idxThk) ?? string.Empty;
            var note = idxNote >= 0 ? cols.ElementAtOrDefault(idxNote) : null;
            var plant = idxPlant >= 0 ? cols.ElementAtOrDefault(idxPlant) : null;

            if (string.IsNullOrWhiteSpace(assetCode)) assetCode = ac;
            if (!string.IsNullOrWhiteSpace(plant)) sourcePlant = plant!;
            if (!DateTime.TryParse(tsStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var ts))
                throw new InvalidOperationException($"Invalid ts: {tsStr}");
            if (!decimal.TryParse(thkStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var thk))
                throw new InvalidOperationException($"Invalid thickness: {thkStr}");

            points.Add(new MeasurementPointDto(lbl, ts, thk, string.IsNullOrWhiteSpace(note) ? null : note));
        }
        if (string.IsNullOrWhiteSpace(assetCode)) throw new InvalidOperationException("asset_code missing in CSV");
        var json = MeasurementBatchPayloadBuilder.BuildJson(points);
        return (assetCode, sourcePlant, json);
    }

    private static MeasurementPointDto ReadPoint(JsonElement element)
    {
        var label = element.GetProperty("label").GetString() ?? throw new InvalidOperationException("label is required");
        var ts = element.GetProperty("ts").GetDateTime();
        var thickness = element.GetProperty("thickness").GetDecimal();

        string? note = null;
        if (element.TryGetProperty("note", out var noteElement))
        {
            note = noteElement.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => noteElement.GetString(),
                _ => noteElement.ToString()
            };
        }

        return new MeasurementPointDto(label, ts, thickness, note);
    }

    private static string[] SplitCsv(string line)
    {
        var res = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (ch == ',' && !inQuotes)
            {
                res.Add(sb.ToString()); sb.Clear();
            }
            else sb.Append(ch);
        }
        res.Add(sb.ToString());
        return res.ToArray();
    }
}
