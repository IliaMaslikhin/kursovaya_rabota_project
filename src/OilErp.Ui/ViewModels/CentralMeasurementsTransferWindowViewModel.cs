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

public sealed partial class CentralMeasurementsTransferWindowViewModel : ObservableObject
{
    private const string DefaultPlantCode = "CENTRAL";

    private readonly string connectionString;
    private bool? hasExtendedColumns;

    public CentralMeasurementsTransferWindowViewModel(string connectionString)
    {
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        title = "Импорт / Экспорт замеров — ЦЕНТРАЛЬНАЯ";
        statusMessage = "Экспорт/импорт выполняется по всем заводам (АНПЗ/КНПЗ/...).";
    }

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
                "Экспорт CSV (ЦЕНТРАЛЬНАЯ)",
                $"центральная_замеры_{DateTime.Now:yyyyMMdd_HHmm}.csv",
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
                "Экспорт JSON (ЦЕНТРАЛЬНАЯ)",
                $"центральная_замеры_{DateTime.Now:yyyyMMdd_HHmm}.json",
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
            var bytes = SimpleXlsxWriter.Build("Замеры", headers, tableRows);
            var ok = await UiFilePicker.SaveBytesAsync(
                "Экспорт Excel (.xlsx) (ЦЕНТРАЛЬНАЯ)",
                $"центральная_замеры_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
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

            var (_, content) = await UiFilePicker.OpenTextAsync("Импорт CSV (ЦЕНТРАЛЬНАЯ)", UiFilePicker.CsvFileType);
            if (string.IsNullOrWhiteSpace(content))
            {
                StatusMessage = "Импорт отменён.";
                return;
            }

            var points = ParseCsv(content);
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

            var (_, content) = await UiFilePicker.OpenTextAsync("Импорт JSON (ЦЕНТРАЛЬНАЯ)", UiFilePicker.JsonFileType);
            if (string.IsNullOrWhiteSpace(content))
            {
                StatusMessage = "Импорт отменён.";
                return;
            }

            var points = ParseJson(content);
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

        var hasExtras = await HasExtendedColumnsAsync(conn);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
                           select
                             mb.source_plant,
                             mb.asset_code,
                             mb.last_date,
                             mb.last_thk,
                             {(hasExtras ? "mb.last_label" : "null::text")} as last_label,
                             {(hasExtras ? "mb.last_note" : "null::text")} as last_note
                           from public.measurement_batches mb
                           order by mb.asset_code, mb.last_date, mb.id
                           """;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var plant = reader.IsDBNull(0) ? DefaultPlantCode : reader.GetString(0);
            var asset = reader.GetString(1);

            var tsValue = reader.GetFieldValue<DateTime>(2);
            if (tsValue.Kind == DateTimeKind.Unspecified) tsValue = DateTime.SpecifyKind(tsValue, DateTimeKind.Utc);
            var ts = new DateTimeOffset(tsValue).ToUniversalTime();

            var thk = reader.GetFieldValue<decimal>(3);
            var label = reader.IsDBNull(4) ? null : reader.GetString(4);
            var note = reader.IsDBNull(5) ? null : reader.GetString(5);

            list.Add(new TransferRow(
                FormatPlant(plant),
                asset,
                string.IsNullOrWhiteSpace(label) ? "T1" : label.Trim(),
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
            .GroupBy(p => p.AssetCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => (AssetCode: g.Key, Points: g.OrderBy(p => p.TimestampUtc).ToArray()))
            .OrderBy(g => g.AssetCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        var hasExtras = await HasExtendedColumnsAsync(conn);

        await using var tx = await conn.BeginTransactionAsync();

        foreach (var group in orderedGroups)
        {
            var assetCode = group.AssetCode.Trim();
            var seedPlant = group.Points.LastOrDefault()?.PlantCode;
            await EnsureAssetExistsAsync(conn, tx, assetCode, seedPlant);

            var existing = await LoadLatestExistingAsync(conn, tx, assetCode);
            DateTime? prevDateUtc = existing?.LastDateUtc;
            decimal? prevThk = existing?.LastThickness;

            foreach (var pt in group.Points)
            {
                var tsUtc = pt.TimestampUtc.UtcDateTime;
                if (prevDateUtc is not null && tsUtc <= prevDateUtc.Value)
                    throw new InvalidOperationException($"asset={assetCode}: дата должна быть строго больше предыдущей (prev={prevDateUtc.Value:O}).");

                if (prevThk is not null && pt.Thickness > prevThk.Value)
                    throw new InvalidOperationException($"asset={assetCode}: толщина не может увеличиваться (prev={prevThk.Value:0.###}).");

                await InsertBatchAsync(conn, tx, hasExtras, pt.PlantCode, assetCode, prevThk, prevDateUtc, pt.Thickness, tsUtc, pt.Label, pt.Note);
                prevDateUtc = tsUtc;
                prevThk = pt.Thickness;
            }
        }

        await tx.CommitAsync();
    }

    private async Task<bool> HasExtendedColumnsAsync(NpgsqlConnection conn)
    {
        if (hasExtendedColumns.HasValue) return hasExtendedColumns.Value;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          select 1
                          from information_schema.columns
                          where table_schema = 'public'
                            and table_name = 'measurement_batches'
                            and column_name in ('last_label','last_note')
                          limit 1
                          """;

        hasExtendedColumns = await cmd.ExecuteScalarAsync() is not null;
        return hasExtendedColumns.Value;
    }

    private static async Task EnsureAssetExistsAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string assetCodeValue, string? plantCode)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          insert into public.assets_global(asset_code, plant_code)
                          values (@code, @plant)
                          on conflict (asset_code) do nothing;
                          """;
        cmd.Parameters.Add("code", NpgsqlDbType.Text).Value = assetCodeValue;
        cmd.Parameters.Add("plant", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(plantCode) ? DBNull.Value : plantCode.Trim();
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<LatestExisting?> LoadLatestExistingAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string assetCodeValue)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          select mb.last_date, mb.last_thk
                          from public.measurement_batches mb
                          where lower(mb.asset_code) = lower(@code)
                          order by mb.last_date desc, mb.id desc
                          limit 1
                          """;
        cmd.Parameters.Add("code", NpgsqlDbType.Text).Value = assetCodeValue;

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var lastDate = reader.GetFieldValue<DateTime>(0);
        if (lastDate.Kind == DateTimeKind.Unspecified) lastDate = DateTime.SpecifyKind(lastDate, DateTimeKind.Utc);

        var lastThk = reader.GetFieldValue<decimal>(1);
        return new LatestExisting(lastDate.ToUniversalTime(), lastThk);
    }

    private sealed record LatestExisting(DateTime LastDateUtc, decimal LastThickness);

    private static async Task InsertBatchAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        bool hasExtras,
        string plantCode,
        string assetCodeValue,
        decimal? prevThk,
        DateTime? prevDateUtc,
        decimal lastThk,
        DateTime lastDateUtc,
        string label,
        string? note)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = hasExtras
            ? """
              insert into public.measurement_batches(
                source_plant, asset_code, prev_thk, prev_date, last_thk, last_date, last_label, last_note
              )
              values (@plant, @asset, @prev_thk, @prev_date, @last_thk, @last_date, @label, @note);
              """
            : """
              insert into public.measurement_batches(
                source_plant, asset_code, prev_thk, prev_date, last_thk, last_date
              )
              values (@plant, @asset, @prev_thk, @prev_date, @last_thk, @last_date);
              """;

        cmd.Parameters.Add("plant", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(plantCode) ? DefaultPlantCode : plantCode.Trim().ToUpperInvariant();
        cmd.Parameters.Add("asset", NpgsqlDbType.Text).Value = assetCodeValue;
        cmd.Parameters.Add("prev_thk", NpgsqlDbType.Numeric).Value = (object?)prevThk ?? DBNull.Value;
        cmd.Parameters.Add("prev_date", NpgsqlDbType.TimestampTz).Value = (object?)prevDateUtc ?? DBNull.Value;
        cmd.Parameters.Add("last_thk", NpgsqlDbType.Numeric).Value = lastThk;
        cmd.Parameters.Add("last_date", NpgsqlDbType.TimestampTz).Value = lastDateUtc;

        if (hasExtras)
        {
            cmd.Parameters.Add("label", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(label) ? DBNull.Value : label.Trim();
            cmd.Parameters.Add("note", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(note) ? DBNull.Value : note.Trim();
        }

        await cmd.ExecuteNonQueryAsync();
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

    private static List<TransferRow> ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Ожидается JSON-массив строк.");

        var list = new List<TransferRow>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var plant = GetString(el, "plant") ?? GetString(el, "source_plant") ?? DefaultPlantCode;
            var asset = GetString(el, "asset_code") ?? throw new InvalidOperationException("В строке отсутствует asset_code.");
            var label = GetString(el, "label") ?? "T1";
            var ts = GetString(el, "ts") ?? throw new InvalidOperationException("В строке отсутствует ts.");
            var thk = el.TryGetProperty("thickness", out var t1) ? t1.ToString() : null;
            if (string.IsNullOrWhiteSpace(thk)) throw new InvalidOperationException("В строке отсутствует thickness.");
            var note = GetString(el, "note");

            var dtoTs = DateTimeOffset.Parse(ts, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
            var dtoThk = decimal.Parse(thk, NumberStyles.Float, CultureInfo.InvariantCulture);

            list.Add(new TransferRow(
                FormatPlant(plant),
                asset.Trim(),
                label.Trim(),
                dtoTs,
                dtoThk,
                string.IsNullOrWhiteSpace(note) ? null : note.Trim()));
        }

        return list;
    }

    private static List<TransferRow> ParseCsv(string csv)
    {
        var lines = csv.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var list = new List<TransferRow>();

        var startIndex = 0;
        if (lines.Length > 0)
        {
            var header = lines[0].Trim();
            if (header.StartsWith("plant", StringComparison.OrdinalIgnoreCase)
                || header.StartsWith("source_plant", StringComparison.OrdinalIgnoreCase))
            {
                startIndex = 1;
            }
        }

        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = SplitCsvLine(line);
            if (parts.Count < 5)
                throw new InvalidOperationException($"CSV: ожидается минимум 5 колонок (plant,asset_code,label,ts,thickness) в строке {i + 1}.");

            var plant = parts[0]?.Trim();
            var asset = parts[1]?.Trim();
            var label = parts[2]?.Trim();
            var ts = parts[3]?.Trim();
            var thk = parts[4]?.Trim();
            var note = parts.Count >= 6 ? parts[5]?.Trim() : null;

            if (string.IsNullOrWhiteSpace(asset)) throw new InvalidOperationException($"CSV: пустой asset_code в строке {i + 1}.");
            if (string.IsNullOrWhiteSpace(ts)) throw new InvalidOperationException($"CSV: пустой ts в строке {i + 1}.");
            if (string.IsNullOrWhiteSpace(thk)) throw new InvalidOperationException($"CSV: пустой thickness в строке {i + 1}.");

            var dtoTs = DateTimeOffset.Parse(ts, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
            var dtoThk = decimal.Parse(thk, NumberStyles.Float, CultureInfo.InvariantCulture);

            list.Add(new TransferRow(
                FormatPlant(string.IsNullOrWhiteSpace(plant) ? DefaultPlantCode : plant),
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

    private static string FormatPlant(string plant)
    {
        if (string.IsNullOrWhiteSpace(plant)) return DefaultPlantCode;
        var upper = plant.Trim().ToUpperInvariant();
        return upper switch
        {
            "KRNPZ" or "KNPZ" => "КНПЗ",
            "ANPZ" => "АНПЗ",
            "CENTRAL" => "Центральная",
            _ => upper
        };
    }
}
