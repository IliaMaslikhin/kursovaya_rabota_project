using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

public sealed partial class CentralMeasurementHistoryWindowViewModel : ObservableObject
{
    private const string DefaultPlantCode = "CENTRAL";

    private readonly string connectionString;
    private readonly string assetCode;
    private bool? hasExtendedColumns;

    public CentralMeasurementHistoryWindowViewModel(string assetCode, string connectionString)
    {
        this.assetCode = assetCode;
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

        Items = new ObservableCollection<CentralMeasurementHistoryItemViewModel>();
        DisplayItems = new ObservableCollection<object>();
        SortOptions = BuildSortOptions();
        selectedSort = SortOptions[0];
        groupByDay = true;
        title = $"История замеров — {assetCode}";
        statusMessage = "Нажмите «Обновить», чтобы загрузить историю.";
    }

    public string AssetCode => assetCode;

    public ObservableCollection<CentralMeasurementHistoryItemViewModel> Items { get; }

    public ObservableCollection<object> DisplayItems { get; }

    public IReadOnlyList<MeasurementSortOption> SortOptions { get; }

    [ObservableProperty] private object? selectedEntry;

    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private string title;

    [ObservableProperty] private string statusMessage;

    [ObservableProperty] private string filterText = string.Empty;

    [ObservableProperty] private string fromUtcText = string.Empty;

    [ObservableProperty] private string toUtcText = string.Empty;

    [ObservableProperty] private bool groupByDay;

    [ObservableProperty] private MeasurementSortOption selectedSort;

    public event Action<bool>? RequestClose;

    partial void OnGroupByDayChanged(bool value) => RebuildDisplayItems();

    partial void OnSelectedSortChanged(MeasurementSortOption value) => _ = RefreshAsync();

    partial void OnIsBusyChanged(bool value)
    {
        RefreshCommand.NotifyCanExecuteChanged();
        ExportCsvCommand.NotifyCanExecuteChanged();
        ExportJsonCommand.NotifyCanExecuteChanged();
        ExportXlsxCommand.NotifyCanExecuteChanged();
        ImportCsvCommand.NotifyCanExecuteChanged();
        ImportJsonCommand.NotifyCanExecuteChanged();
    }

    private bool CanRefresh() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Загрузка истории...";
            Items.Clear();
            DisplayItems.Clear();

            if (!TryParseUtcDate(FromUtcText, out var fromUtc, out var fromError))
            {
                StatusMessage = fromError;
                return;
            }

            if (!TryParseUtcDate(ToUtcText, out var toUtc, out var toError))
            {
                StatusMessage = toError;
                return;
            }

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var hasExtras = await HasExtendedColumnsAsync(conn);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                               select
                                 mb.id,
                                 mb.source_plant,
                                 mb.last_date,
                                 mb.last_thk,
                                 {(hasExtras ? "mb.last_label" : "null::text")} as last_label,
                                 {(hasExtras ? "mb.last_note" : "null::text")} as last_note
                               from public.measurement_batches mb
                               where lower(mb.asset_code) = lower(@code)
                                 and (@from is null or mb.last_date >= @from)
                                 and (@to is null or mb.last_date <= @to)
                                 and (
                                   @q is null
                                   or coalesce(mb.source_plant,'') ilike @q
                                   or coalesce({(hasExtras ? "mb.last_label" : "''")}, '') ilike @q
                                   or coalesce({(hasExtras ? "mb.last_note" : "''")}, '') ilike @q
                                 )
                               order by {SelectedSort.OrderBySql}
                               limit 800
                               """;

            cmd.Parameters.Add("code", NpgsqlDbType.Text).Value = assetCode.Trim();
            cmd.Parameters.Add("from", NpgsqlDbType.TimestampTz).Value = (object?)fromUtc ?? DBNull.Value;
            cmd.Parameters.Add("to", NpgsqlDbType.TimestampTz).Value = (object?)toUtc ?? DBNull.Value;
            cmd.Parameters.Add("q", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(FilterText) ? DBNull.Value : $"%{FilterText.Trim()}%";

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt64(0);
                var plant = reader.IsDBNull(1) ? "—" : reader.GetString(1);

                var tsValue = reader.GetFieldValue<DateTime>(2);
                if (tsValue.Kind == DateTimeKind.Unspecified) tsValue = DateTime.SpecifyKind(tsValue, DateTimeKind.Utc);
                var ts = new DateTimeOffset(tsValue).ToUniversalTime();

                var thickness = reader.GetFieldValue<decimal>(3);
                var label = reader.IsDBNull(4) ? null : reader.GetString(4);
                var note = reader.IsDBNull(5) ? null : reader.GetString(5);

                Items.Add(new CentralMeasurementHistoryItemViewModel(id, FormatPlant(plant), ts, thickness, label, note));
            }

            RebuildDisplayItems();
            StatusMessage = $"Загружено: {Items.Count}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task ExportCsvAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Готовим CSV...";

            var csv = BuildCsv(Items);
            var ok = await UiFilePicker.SaveTextAsync(
                "Экспорт CSV",
                $"{assetCode}_центральная_замеры.csv",
                csv,
                UiFilePicker.CsvFileType);

            StatusMessage = ok ? "CSV сохранён." : "Экспорт отменён.";
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

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task ExportJsonAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Готовим JSON...";

            var json = BuildJson(Items);
            var ok = await UiFilePicker.SaveTextAsync(
                "Экспорт JSON",
                $"{assetCode}_центральная_замеры.json",
                json,
                UiFilePicker.JsonFileType);

            StatusMessage = ok ? "JSON сохранён." : "Экспорт отменён.";
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

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task ExportXlsxAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Готовим XLSX...";

            var (headers, rows) = BuildTable(Items);
            var bytes = SimpleXlsxWriter.Build("Замеры", headers, rows);
            var ok = await UiFilePicker.SaveBytesAsync(
                "Экспорт Excel (.xlsx)",
                $"{assetCode}_центральная_замеры.xlsx",
                bytes,
                UiFilePicker.XlsxFileType);

            StatusMessage = ok ? "XLSX сохранён." : "Экспорт отменён.";
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

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task ImportCsvAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Открываем CSV...";

            var (_, content) = await UiFilePicker.OpenTextAsync("Импорт CSV", UiFilePicker.CsvFileType);
            if (string.IsNullOrWhiteSpace(content))
            {
                StatusMessage = "Импорт отменён.";
                return;
            }

            var points = ParseCsv(content);
            StatusMessage = $"Импортируем точек: {points.Count}...";

            await InsertPointsAsync(points);
            await RefreshAsync();

            StatusMessage = $"Импортировано точек: {points.Count}.";
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

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task ImportJsonAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Открываем JSON...";

            var (_, content) = await UiFilePicker.OpenTextAsync("Импорт JSON", UiFilePicker.JsonFileType);
            if (string.IsNullOrWhiteSpace(content))
            {
                StatusMessage = "Импорт отменён.";
                return;
            }

            var points = ParseJson(content);
            StatusMessage = $"Импортируем точек: {points.Count}...";

            await InsertPointsAsync(points);
            await RefreshAsync();

            StatusMessage = $"Импортировано точек: {points.Count}.";
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

    private async Task InsertPointsAsync(IReadOnlyList<ImportedPoint> points)
    {
        if (points.Count == 0) return;

        var ordered = points
            .OrderBy(p => p.TimestampUtc)
            .ThenBy(p => p.PlantCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var seedPlant = ordered.LastOrDefault()?.PlantCode;
        await EnsureAssetExistsAsync(conn, tx, assetCode.Trim(), seedPlant);

        var hasExtras = await HasExtendedColumnsAsync(conn);
        var existing = await LoadLatestExistingAsync(conn, tx, assetCode.Trim());

        DateTime? prevDateUtc = existing?.LastDateUtc;
        decimal? prevThk = existing?.LastThickness;

        foreach (var pt in ordered)
        {
            var tsUtc = pt.TimestampUtc.UtcDateTime;
            if (prevDateUtc is not null && tsUtc <= prevDateUtc.Value)
                throw new InvalidOperationException($"Точка {pt.Label}: дата должна быть больше предыдущей (prev={prevDateUtc.Value:O}).");

            if (prevThk is not null && pt.Thickness > prevThk.Value)
                throw new InvalidOperationException($"Точка {pt.Label}: толщина не может увеличиваться (prev={prevThk.Value:0.###}).");

            await InsertBatchAsync(conn, tx, hasExtras, pt.PlantCode, assetCode.Trim(), prevThk, prevDateUtc, pt.Thickness, tsUtc, pt.Label, pt.Note);

            prevDateUtc = tsUtc;
            prevThk = pt.Thickness;
        }

        await tx.CommitAsync();
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

    private sealed record ImportedPoint(string PlantCode, string Label, DateTimeOffset TimestampUtc, decimal Thickness, string? Note);

    private static string BuildCsv(IEnumerable<CentralMeasurementHistoryItemViewModel> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("plant,label,ts,thickness,note");

        foreach (var p in items.OrderBy(i => i.TimestampUtc))
        {
            sb.Append(EscapeCsv(p.Plant));
            sb.Append(',');
            sb.Append(EscapeCsv(p.LabelDisplay));
            sb.Append(',');
            sb.Append(EscapeCsv(p.TimestampUtc.ToString("O", CultureInfo.InvariantCulture)));
            sb.Append(',');
            sb.Append(EscapeCsv(p.Thickness.ToString("0.###", CultureInfo.InvariantCulture)));
            sb.Append(',');
            sb.Append(EscapeCsv(p.Note ?? string.Empty));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static string BuildJson(IEnumerable<CentralMeasurementHistoryItemViewModel> items)
    {
        var points = items
            .OrderBy(p => p.TimestampUtc)
            .Select(p => new Dictionary<string, object?>
            {
                ["plant"] = p.Plant,
                ["label"] = p.LabelDisplay,
                ["ts"] = p.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
                ["thickness"] = p.Thickness,
                ["note"] = p.Note
            })
            .ToArray();

        return JsonSerializer.Serialize(points, new JsonSerializerOptions { WriteIndented = true });
    }

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildTable(IEnumerable<CentralMeasurementHistoryItemViewModel> items)
    {
        var ordered = items
            .OrderBy(p => p.TimestampUtc)
            .ToArray();

        var headers = new[] { "plant", "label", "ts", "thickness", "note" };
        var rows = ordered
            .Select(it => (IReadOnlyList<string>)new[]
            {
                it.Plant,
                it.LabelDisplay,
                it.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
                it.Thickness.ToString("0.###", CultureInfo.InvariantCulture),
                it.Note ?? string.Empty
            })
            .ToArray();

        return (headers, rows);
    }

    private static List<ImportedPoint> ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Ожидается JSON-массив точек.");

        var list = new List<ImportedPoint>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var label = el.TryGetProperty("label", out var lbl) ? lbl.GetString() : null;
            var ts = el.TryGetProperty("ts", out var tsEl) ? tsEl.GetString() : null;
            var thk = el.TryGetProperty("thickness", out var thkEl) ? thkEl.ToString() : null;
            var note = el.TryGetProperty("note", out var noteEl) ? (noteEl.ValueKind == JsonValueKind.Null ? null : noteEl.ToString()) : null;

            var plant = el.TryGetProperty("plant", out var p1) ? p1.GetString() : null;
            if (string.IsNullOrWhiteSpace(plant) && el.TryGetProperty("source_plant", out var p2)) plant = p2.GetString();

            if (string.IsNullOrWhiteSpace(ts)) throw new InvalidOperationException("В точке отсутствует ts.");
            if (string.IsNullOrWhiteSpace(thk)) throw new InvalidOperationException("В точке отсутствует thickness.");

            var dtoTs = DateTimeOffset.Parse(ts, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
            var dtoThk = decimal.Parse(thk, NumberStyles.Float, CultureInfo.InvariantCulture);

            list.Add(new ImportedPoint(
                string.IsNullOrWhiteSpace(plant) ? DefaultPlantCode : plant.Trim(),
                string.IsNullOrWhiteSpace(label) ? "T1" : label.Trim(),
                dtoTs,
                dtoThk,
                string.IsNullOrWhiteSpace(note) ? null : note.Trim()));
        }

        return list;
    }

    private static List<ImportedPoint> ParseCsv(string csv)
    {
        var lines = csv.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var list = new List<ImportedPoint>();

        var startIndex = 0;
        var headerHasPlant = false;
        if (lines.Length > 0)
        {
            var header = lines[0].Trim();
            if (header.StartsWith("plant", StringComparison.OrdinalIgnoreCase)
                || header.StartsWith("source_plant", StringComparison.OrdinalIgnoreCase))
            {
                startIndex = 1;
                headerHasPlant = true;
            }
            else if (header.StartsWith("label", StringComparison.OrdinalIgnoreCase))
            {
                startIndex = 1;
            }
        }

        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var parts = SplitCsvLine(line);
            if (parts.Count < 3)
                throw new InvalidOperationException($"CSV: ожидается минимум 3 колонки (label,ts,thickness) в строке {i + 1}.");

            string? plant = null;
            var labelIndex = 0;
            if (headerHasPlant)
            {
                plant = parts[0]?.Trim();
                labelIndex = 1;
                if (parts.Count < 4)
                    throw new InvalidOperationException($"CSV: ожидается минимум 4 колонки (plant,label,ts,thickness) в строке {i + 1}.");
            }

            var label = parts[labelIndex]?.Trim();
            var ts = parts[labelIndex + 1]?.Trim();
            var thk = parts[labelIndex + 2]?.Trim();
            var note = parts.Count > labelIndex + 3 ? parts[labelIndex + 3]?.Trim() : null;

            if (string.IsNullOrWhiteSpace(ts)) throw new InvalidOperationException($"CSV: пустой ts в строке {i + 1}.");
            if (string.IsNullOrWhiteSpace(thk)) throw new InvalidOperationException($"CSV: пустой thickness в строке {i + 1}.");

            var dtoTs = DateTimeOffset.Parse(ts, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
            var dtoThk = decimal.Parse(thk, NumberStyles.Float, CultureInfo.InvariantCulture);

            list.Add(new ImportedPoint(
                string.IsNullOrWhiteSpace(plant) ? DefaultPlantCode : plant.Trim(),
                string.IsNullOrWhiteSpace(label) ? "T1" : label.Trim(),
                dtoTs,
                dtoThk,
                string.IsNullOrWhiteSpace(note) ? null : note.Trim()));
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

    private void RebuildDisplayItems()
    {
        DisplayItems.Clear();

        if (!GroupByDay)
        {
            foreach (var it in Items)
            {
                DisplayItems.Add(it);
            }

            return;
        }

        var groups = Items
            .GroupBy(i => i.TimestampUtc.ToLocalTime().Date)
            .OrderByDescending(g => g.Key);

        foreach (var g in groups)
        {
            DisplayItems.Add(new CentralMeasurementHistoryGroupHeaderViewModel(g.Key, g.Count()));
            foreach (var it in g.OrderByDescending(i => i.TimestampUtc))
            {
                DisplayItems.Add(it);
            }
        }
    }

    private static bool TryParseUtcDate(string text, out DateTime? utcDateTime, out string error)
    {
        utcDateTime = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return true;

        if (!DateTimeOffset.TryParse(
                text.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
        {
            error = "Неверный формат даты. Пример: 2025-12-18 или 2025-12-18T00:00:00Z";
            return false;
        }

        utcDateTime = dto.UtcDateTime;
        return true;
    }

    private static MeasurementSortOption[] BuildSortOptions() =>
        new[]
        {
            new MeasurementSortOption("ts_desc", "Дата (новые)", "mb.last_date desc, mb.id desc"),
            new MeasurementSortOption("ts_asc", "Дата (старые)", "mb.last_date asc, mb.id asc"),
            new MeasurementSortOption("plant", "Завод", "mb.source_plant asc nulls last, mb.last_date desc, mb.id desc"),
            new MeasurementSortOption("thk_desc", "Толщина (убывание)", "mb.last_thk desc, mb.last_date desc, mb.id desc"),
            new MeasurementSortOption("thk_asc", "Толщина (возрастание)", "mb.last_thk asc, mb.last_date desc, mb.id desc"),
            new MeasurementSortOption("label", "Метка", "mb.last_label asc nulls last, mb.last_date desc, mb.id desc")
        };

    private static string FormatPlant(string plant)
    {
        if (string.IsNullOrWhiteSpace(plant)) return "—";
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

public sealed record CentralMeasurementHistoryItemViewModel(
    long Id,
    string Plant,
    DateTimeOffset TimestampUtc,
    decimal Thickness,
    string? Label,
    string? Note)
{
    public string TimestampLocal => TimestampUtc.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public string LabelDisplay => string.IsNullOrWhiteSpace(Label) ? "—" : Label.Trim();

    public string ThicknessDisplay => Thickness.ToString("0.###", CultureInfo.InvariantCulture);
}

public sealed record CentralMeasurementHistoryGroupHeaderViewModel(DateTime Day, int Count)
{
    public string Title => $"{Day:dd.MM.yyyy} ({Count})";
}
