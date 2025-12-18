using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npgsql;
using NpgsqlTypes;
using OilErp.Ui.Services;

namespace OilErp.Ui.ViewModels;

public sealed partial class PlantMeasurementsTransferWindowViewModel : ObservableObject
{
    private readonly string connectionString;
    private readonly string plantCode;

    public PlantMeasurementsTransferWindowViewModel(string plantCode, string connectionString)
    {
        this.plantCode = plantCode;
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        title = $"Импорт / Экспорт замеров — {plantCode}";
        statusMessage = "Экспорт/импорт выполняется только для текущей БД завода.";
    }

    public string PlantCode => plantCode;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string title;
    [ObservableProperty] private string statusMessage;

    public event Action<bool>? RequestClose;

    private bool CanRun() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRun))]
    public async Task ExportCsvAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Загружаем данные и готовим CSV...";

            var rows = await LoadAllAsync();
            var csv = BuildCsv(rows);
            var ok = await UiFilePicker.SaveTextAsync(
                $"Экспорт CSV ({plantCode})",
                $"{plantCode.ToLowerInvariant()}_measurements_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                csv,
                UiFilePicker.CsvFileType);

            StatusMessage = ok ? $"CSV сохранён (строк={rows.Count})." : "Экспорт отменён.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка экспорта: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    public async Task ExportJsonAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Загружаем данные и готовим JSON...";

            var rows = await LoadAllAsync();
            var json = BuildJson(rows);
            var ok = await UiFilePicker.SaveTextAsync(
                $"Экспорт JSON ({plantCode})",
                $"{plantCode.ToLowerInvariant()}_measurements_{DateTime.Now:yyyyMMdd_HHmm}.json",
                json,
                UiFilePicker.JsonFileType);

            StatusMessage = ok ? $"JSON сохранён (строк={rows.Count})." : "Экспорт отменён.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка экспорта: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    public async Task ExportXlsxAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Загружаем данные и готовим XLSX...";

            var rows = await LoadAllAsync();
            var (headers, tableRows) = BuildTable(rows);
            var bytes = SimpleXlsxWriter.Build("Measurements", headers, tableRows);
            var ok = await UiFilePicker.SaveBytesAsync(
                $"Экспорт Excel (.xlsx) ({plantCode})",
                $"{plantCode.ToLowerInvariant()}_measurements_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                bytes,
                UiFilePicker.XlsxFileType);

            StatusMessage = ok ? $"XLSX сохранён (строк={rows.Count})." : "Экспорт отменён.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка экспорта: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    public async Task ImportCsvAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Открываем CSV...";

            var (_, content) = await UiFilePicker.OpenTextAsync($"Импорт CSV ({plantCode})", UiFilePicker.CsvFileType);
            if (string.IsNullOrWhiteSpace(content))
            {
                StatusMessage = "Импорт отменён.";
                return;
            }

            var points = ParseCsv(content, plantCode);
            StatusMessage = $"Импортируем строк: {points.Count}...";

            await InsertPointsAsync(points);
            StatusMessage = $"Импорт завершён (строк={points.Count}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка импорта: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    public async Task ImportJsonAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Открываем JSON...";

            var (_, content) = await UiFilePicker.OpenTextAsync($"Импорт JSON ({plantCode})", UiFilePicker.JsonFileType);
            if (string.IsNullOrWhiteSpace(content))
            {
                StatusMessage = "Импорт отменён.";
                return;
            }

            var points = ParseJson(content, plantCode);
            StatusMessage = $"Импортируем строк: {points.Count}...";

            await InsertPointsAsync(points);
            StatusMessage = $"Импорт завершён (строк={points.Count}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка импорта: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Close() => RequestClose?.Invoke(true);

    private async Task<List<TransferRow>> LoadAllAsync()
    {
        var list = new List<TransferRow>();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          select a.asset_code, mp.label, m.ts, m.thickness, m.note
                          from public.measurements m
                          join public.measurement_points mp on mp.id = m.point_id
                          join public.assets_local a on a.id = mp.asset_id
                          order by a.asset_code, m.ts, m.id
                          """;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var asset = reader.GetString(0);
            var label = reader.GetString(1);

            var tsValue = reader.GetFieldValue<DateTime>(2);
            if (tsValue.Kind == DateTimeKind.Unspecified) tsValue = DateTime.SpecifyKind(tsValue, DateTimeKind.Utc);
            var ts = new DateTimeOffset(tsValue).ToUniversalTime();

            var thk = reader.GetFieldValue<decimal>(3);
            var note = reader.IsDBNull(4) ? null : reader.GetString(4);

            list.Add(new TransferRow(
                plantCode,
                asset,
                label,
                ts,
                thk,
                string.IsNullOrWhiteSpace(note) ? null : note.Trim()));
        }

        return list;
    }

    private async Task InsertPointsAsync(IReadOnlyList<TransferRow> points)
    {
        if (points.Count == 0) return;

        var orderedGroups = points
            .Where(p => !string.IsNullOrWhiteSpace(p.AssetCode))
            .GroupBy(p => p.AssetCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => (AssetCode: g.Key, Points: g.OrderBy(p => p.TimestampUtc).ToArray()))
            .OrderBy(g => g.AssetCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var group in orderedGroups)
        {
            var payload = group.Points
                .Select(p => new Dictionary<string, object?>
                {
                    ["label"] = p.Label,
                    ["ts"] = p.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
                    ["thickness"] = p.Thickness,
                    ["note"] = p.Note
                })
                .ToArray();

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            await InsertBatchAsync(group.AssetCode, json);
        }
    }

    private async Task InsertBatchAsync(string assetCodeValue, string pointsJsonArray)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select public.sp_insert_measurement_batch(@asset, @points::jsonb, @plant)";
        cmd.Parameters.Add("asset", NpgsqlDbType.Text).Value = assetCodeValue.Trim();
        cmd.Parameters.Add("points", NpgsqlDbType.Text).Value = pointsJsonArray;
        cmd.Parameters.Add("plant", NpgsqlDbType.Text).Value = plantCode;
        await cmd.ExecuteScalarAsync();
    }

    private sealed record TransferRow(string PlantCode, string AssetCode, string Label, DateTimeOffset TimestampUtc, decimal Thickness, string? Note);

    private static string BuildCsv(IReadOnlyList<TransferRow> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("plant,asset_code,label,ts,thickness,note");

        foreach (var it in items)
        {
            sb.Append(EscapeCsv(it.PlantCode));
            sb.Append(',');
            sb.Append(EscapeCsv(it.AssetCode));
            sb.Append(',');
            sb.Append(EscapeCsv(it.Label));
            sb.Append(',');
            sb.Append(EscapeCsv(it.TimestampUtc.ToString("O", CultureInfo.InvariantCulture)));
            sb.Append(',');
            sb.Append(EscapeCsv(it.Thickness.ToString("0.###", CultureInfo.InvariantCulture)));
            sb.Append(',');
            sb.Append(EscapeCsv(it.Note ?? string.Empty));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildJson(IReadOnlyList<TransferRow> items)
    {
        var points = items
            .Select(p => new Dictionary<string, object?>
            {
                ["plant"] = p.PlantCode,
                ["asset_code"] = p.AssetCode,
                ["label"] = p.Label,
                ["ts"] = p.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
                ["thickness"] = p.Thickness,
                ["note"] = p.Note
            })
            .ToArray();

        return JsonSerializer.Serialize(points, new JsonSerializerOptions { WriteIndented = true });
    }

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildTable(IReadOnlyList<TransferRow> items)
    {
        var headers = new[] { "plant", "asset_code", "label", "ts", "thickness", "note" };
        var rows = items
            .Select(it => (IReadOnlyList<string>)new[]
            {
                it.PlantCode,
                it.AssetCode,
                it.Label,
                it.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
                it.Thickness.ToString("0.###", CultureInfo.InvariantCulture),
                it.Note ?? string.Empty
            })
            .ToArray();

        return (headers, rows);
    }

    private static List<TransferRow> ParseJson(string json, string plantCode)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Ожидается JSON-массив строк.");

        var list = new List<TransferRow>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var sourcePlant = GetString(el, "plant") ?? GetString(el, "source_plant");
            if (!string.IsNullOrWhiteSpace(sourcePlant) && !IsSamePlant(sourcePlant, plantCode))
            {
                continue;
            }

            var asset = GetString(el, "asset_code") ?? throw new InvalidOperationException("В строке отсутствует asset_code.");
            var label = GetString(el, "label") ?? "T1";
            var ts = GetString(el, "ts") ?? throw new InvalidOperationException("В строке отсутствует ts.");
            var thk = el.TryGetProperty("thickness", out var t1) ? t1.ToString() : null;
            if (string.IsNullOrWhiteSpace(thk)) throw new InvalidOperationException("В строке отсутствует thickness.");
            var note = GetString(el, "note");

            var dtoTs = DateTimeOffset.Parse(ts, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
            var dtoThk = decimal.Parse(thk, NumberStyles.Float, CultureInfo.InvariantCulture);

            list.Add(new TransferRow(
                plantCode,
                asset.Trim(),
                label.Trim(),
                dtoTs,
                dtoThk,
                string.IsNullOrWhiteSpace(note) ? null : note.Trim()));
        }

        return list;
    }

    private static List<TransferRow> ParseCsv(string csv, string plantCode)
    {
        var lines = csv.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var list = new List<TransferRow>();

        var startIndex = 0;
        var headerHasPlant = false;
        if (lines.Length > 0)
        {
            var header = lines[0].Trim();
            if (header.StartsWith("plant", StringComparison.OrdinalIgnoreCase))
            {
                startIndex = 1;
                headerHasPlant = true;
            }
            else if (header.StartsWith("asset_code", StringComparison.OrdinalIgnoreCase))
            {
                startIndex = 1;
            }
        }

        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = SplitCsvLine(line);
            if (parts.Count < 4)
                throw new InvalidOperationException($"CSV: ожидается минимум 4 колонки (asset_code,label,ts,thickness) в строке {i + 1}.");

            var offset = 0;
            string? sourcePlant = null;
            if (headerHasPlant)
            {
                sourcePlant = parts[0]?.Trim();
                offset = 1;
            }

            var asset = parts[offset + 0]?.Trim();
            var label = parts[offset + 1]?.Trim();
            var ts = parts[offset + 2]?.Trim();
            var thk = parts[offset + 3]?.Trim();
            var note = parts.Count > offset + 4 ? parts[offset + 4]?.Trim() : null;

            if (!string.IsNullOrWhiteSpace(sourcePlant) && !IsSamePlant(sourcePlant, plantCode))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(asset)) throw new InvalidOperationException($"CSV: пустой asset_code в строке {i + 1}.");
            if (string.IsNullOrWhiteSpace(ts)) throw new InvalidOperationException($"CSV: пустой ts в строке {i + 1}.");
            if (string.IsNullOrWhiteSpace(thk)) throw new InvalidOperationException($"CSV: пустой thickness в строке {i + 1}.");

            var dtoTs = DateTimeOffset.Parse(ts, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
            var dtoThk = decimal.Parse(thk, NumberStyles.Float, CultureInfo.InvariantCulture);

            list.Add(new TransferRow(
                plantCode,
                asset,
                string.IsNullOrWhiteSpace(label) ? "T1" : label,
                dtoTs,
                dtoThk,
                string.IsNullOrWhiteSpace(note) ? null : note));
        }

        return list;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                        continue;
                    }

                    inQuotes = false;
                    continue;
                }

                sb.Append(ch);
                continue;
            }

            if (ch == '"')
            {
                inQuotes = true;
                continue;
            }

            if (ch == ',')
            {
                result.Add(sb.ToString());
                sb.Clear();
                continue;
            }

            sb.Append(ch);
        }

        result.Add(sb.ToString());
        return result;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static string? GetString(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Null) return null;
        return value.ToString();
    }

    private static bool IsSamePlant(string a, string b)
    {
        var left = NormalizePlant(a);
        var right = NormalizePlant(b);
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePlant(string value)
    {
        var upper = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (upper == "KRNPZ") return "KNPZ";
        return upper;
    }
}
