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
using OilErp.Ui.Views;

namespace OilErp.Ui.ViewModels;

public sealed partial class PlantMeasurementHistoryWindowViewModel : ObservableObject
{
    private readonly string connectionString;
    private readonly string plantCode;
    private readonly string assetCode;

    public PlantMeasurementHistoryWindowViewModel(string plantCode, string assetCode, string connectionString)
    {
        this.plantCode = plantCode;
        this.assetCode = assetCode;
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        Items = new ObservableCollection<PlantMeasurementHistoryItemViewModel>();
        DisplayItems = new ObservableCollection<object>();
        SortOptions = BuildSortOptions();
        selectedSort = SortOptions[0];
        title = $"История замеров — {assetCode}";
        statusMessage = "Нажмите «Обновить», чтобы загрузить историю.";
    }

    public string PlantCode => plantCode;

    public string PlantCodeDisplay => plantCode switch
    {
        "ANPZ" => "АНПЗ",
        "KNPZ" or "KRNPZ" => "КНПЗ",
        _ => plantCode
    };

    public string AssetCode => assetCode;

    public ObservableCollection<PlantMeasurementHistoryItemViewModel> Items { get; }

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

    partial void OnSelectedEntryChanged(object? value)
    {
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    partial void OnGroupByDayChanged(bool value)
    {
        RebuildDisplayItems();
    }

    partial void OnIsBusyChanged(bool value)
    {
        RefreshCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
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

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                               select m.id, mp.label, m.ts, m.thickness, m.note
                               from public.measurements m
                               join public.measurement_points mp on mp.id = m.point_id
                               join public.assets_local a on a.id = mp.asset_id
                               where a.asset_code = @code
                                 and (@from is null or m.ts >= @from)
                                 and (@to is null or m.ts <= @to)
                                 and (@q is null or mp.label ilike @q or coalesce(m.note,'') ilike @q)
                               order by {SelectedSort.OrderBySql}
                               limit 500
                               """;
            cmd.Parameters.AddWithValue("code", assetCode);
            cmd.Parameters.Add("from", NpgsqlDbType.TimestampTz).Value = (object?)fromUtc ?? DBNull.Value;
            cmd.Parameters.Add("to", NpgsqlDbType.TimestampTz).Value = (object?)toUtc ?? DBNull.Value;
            cmd.Parameters.Add("q", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(FilterText) ? DBNull.Value : $"%{FilterText.Trim()}%";

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt64(0);
                var label = reader.GetString(1);

                var tsValue = reader.GetFieldValue<DateTime>(2);
                if (tsValue.Kind == DateTimeKind.Unspecified) tsValue = DateTime.SpecifyKind(tsValue, DateTimeKind.Utc);
                var ts = new DateTimeOffset(tsValue).ToUniversalTime();

                var thickness = reader.GetFieldValue<decimal>(3);
                var note = reader.IsDBNull(4) ? null : reader.GetString(4);

                Items.Add(new PlantMeasurementHistoryItemViewModel(id, label, ts, thickness, note));
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

    private PlantMeasurementHistoryItemViewModel? SelectedMeasurement => SelectedEntry as PlantMeasurementHistoryItemViewModel;

    private bool CanEditOrDelete() => !IsBusy && SelectedMeasurement is not null;

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    public async Task EditAsync()
    {
        var item = SelectedMeasurement;
        if (item is null) return;

        var vm = new PlantMeasurementEditWindowViewModel(
            "Изменить замер",
            plantCode,
            assetCode,
            item.Label,
            initialThickness: (double)item.Thickness,
            initialNote: item.Note,
            isLabelReadOnly: true);

        var dialog = new PlantMeasurementEditWindow { DataContext = vm };
        var result = await UiDialogHost.ShowDialogAsync<PlantMeasurementEditResult?>(dialog);
        if (result is null) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Сохраняем изменения...";

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            await UpdateMeasurementAsync(conn, tx, item.Id, result.Thickness, result.Note);
            await EnqueueBatchToCentralAsync(conn, tx);

            await tx.CommitAsync();
            StatusMessage = "Изменения сохранены.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка изменения: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDelete))]
    public async Task DeleteAsync()
    {
        var item = SelectedMeasurement;
        if (item is null) return;

        var confirmVm = new ConfirmDialogViewModel(
            "Удалить замер",
            $"Удалить замер?\n\n" +
            $"дата: {item.TimestampUtc:O}\n" +
            $"метка: {item.Label}\n" +
            $"толщина: {item.Thickness:0.###}",
            confirmText: "Удалить",
            cancelText: "Отмена");

        var confirmDialog = new ConfirmDialogWindow { DataContext = confirmVm };
        var confirm = await UiDialogHost.ShowDialogAsync<bool?>(confirmDialog);
        if (confirm != true) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Удаляем...";

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            await DeleteMeasurementAsync(conn, tx, item.Id);
            await EnqueueBatchToCentralAsync(conn, tx);

            await tx.CommitAsync();
            StatusMessage = "Удалено.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка удаления: {ex.Message}";
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
                $"{assetCode}_{plantCode}_замеры.csv",
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
                $"{assetCode}_{plantCode}_замеры.json",
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
                $"{assetCode}_{plantCode}_замеры.xlsx",
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
            var json = JsonSerializer.Serialize(points, new JsonSerializerOptions { WriteIndented = true });

            StatusMessage = $"Импортируем точек: {points.Count}...";
            await InsertBatchAsync(json);

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
            var json = JsonSerializer.Serialize(points, new JsonSerializerOptions { WriteIndented = true });

            StatusMessage = $"Импортируем точек: {points.Count}...";
            await InsertBatchAsync(json);

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

    private async Task InsertBatchAsync(string pointsJsonArray)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select public.sp_insert_measurement_batch(@asset, @points::jsonb, @plant)";
        cmd.Parameters.AddWithValue("asset", assetCode);
        cmd.Parameters.AddWithValue("points", pointsJsonArray);
        cmd.Parameters.AddWithValue("plant", plantCode);
        await cmd.ExecuteScalarAsync();
    }

    private async Task EnqueueBatchToCentralAsync(NpgsqlConnection conn, NpgsqlTransaction tx)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          with ordered as (
                            select m.ts, m.thickness, mp.label, m.note, row_number() over (order by m.ts desc, m.id desc) as rn
                            from public.measurements m
                            join public.measurement_points mp on mp.id = m.point_id
                            join public.assets_local a on a.id = mp.asset_id
                            where a.asset_code = @code
                          )
                          select
                            max(ts) filter (where rn = 2) as prev_date,
                            max(thickness) filter (where rn = 2) as prev_thk,
                            max(ts) filter (where rn = 1) as last_date,
                            max(thickness) filter (where rn = 1) as last_thk,
                            max(label) filter (where rn = 1) as last_label,
                            max(note) filter (where rn = 1) as last_note
                          from ordered
                          """;
        cmd.Parameters.AddWithValue("code", assetCode);

        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        var prevDate = reader.IsDBNull(0) ? (DateTime?)null : reader.GetFieldValue<DateTime>(0);
        var prevThk = reader.IsDBNull(1) ? (decimal?)null : reader.GetFieldValue<decimal>(1);
        var lastDate = reader.IsDBNull(2) ? (DateTime?)null : reader.GetFieldValue<DateTime>(2);
        var lastThk = reader.IsDBNull(3) ? (decimal?)null : reader.GetFieldValue<decimal>(3);
        var lastLabel = reader.IsDBNull(4) ? (string?)null : reader.GetString(4);
        var lastNote = reader.IsDBNull(5) ? (string?)null : reader.GetString(5);

        await reader.CloseAsync();

        if (lastDate is null || lastThk is null)
        {
            StatusMessage = "Локально обновлено, но отправлять в центральную базу нечего (нет замеров).";
            return;
        }

        await using var enqueue = conn.CreateCommand();
        enqueue.Transaction = tx;
        enqueue.CommandText = """
                              insert into central_ft.measurement_batches(source_plant, asset_code, prev_thk, prev_date, last_thk, last_date, last_label, last_note)
                              values (@plant, @code, @prev_thk, @prev_date, @last_thk, @last_date, @last_label, @last_note)
                              """;
        enqueue.Parameters.AddWithValue("plant", plantCode);
        enqueue.Parameters.AddWithValue("code", assetCode);
        enqueue.Parameters.AddWithValue("prev_thk", (object?)prevThk ?? DBNull.Value);
        enqueue.Parameters.AddWithValue("prev_date", (object?)prevDate ?? DBNull.Value);
        enqueue.Parameters.AddWithValue("last_thk", lastThk.Value);
        enqueue.Parameters.AddWithValue("last_date", lastDate.Value);
        enqueue.Parameters.AddWithValue("last_label", (object?)lastLabel ?? DBNull.Value);
        enqueue.Parameters.AddWithValue("last_note", (object?)lastNote ?? DBNull.Value);
        await enqueue.ExecuteNonQueryAsync();
    }

    private static async Task UpdateMeasurementAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long id, double thickness, string? note)
    {
        var thicknessValue = Math.Round((decimal)thickness, 3, MidpointRounding.AwayFromZero);
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "update public.measurements set thickness=@thk, note=@note where id=@id";
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("thk", thicknessValue);
        cmd.Parameters.AddWithValue("note", (object?)note ?? DBNull.Value);

        var affected = await cmd.ExecuteNonQueryAsync();
        if (affected != 1)
            throw new InvalidOperationException($"expected to update 1 row, updated {affected}");
    }

    private static async Task DeleteMeasurementAsync(NpgsqlConnection conn, NpgsqlTransaction tx, long id)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "delete from public.measurements where id=@id";
        cmd.Parameters.AddWithValue("id", id);

        var affected = await cmd.ExecuteNonQueryAsync();
        if (affected != 1)
            throw new InvalidOperationException($"expected to delete 1 row, deleted {affected}");
    }

    private static string BuildCsv(IEnumerable<PlantMeasurementHistoryItemViewModel> items)
    {
        var (headers, rows) = BuildTable(items);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(EscapeCsv)));
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
        }
        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    private static string BuildJson(IEnumerable<PlantMeasurementHistoryItemViewModel> items)
    {
        var points = items
            .OrderBy(p => p.TimestampUtc)
            .ThenBy(p => p.Label, StringComparer.Ordinal)
            .Select(p => new Dictionary<string, object?>
            {
                ["label"] = p.Label,
                ["ts"] = p.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
                ["thickness"] = p.Thickness,
                ["note"] = p.Note
            })
            .ToArray();

        return JsonSerializer.Serialize(points, new JsonSerializerOptions { WriteIndented = true });
    }

    private static (IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows) BuildTable(IEnumerable<PlantMeasurementHistoryItemViewModel> items)
    {
        var ordered = items
            .OrderBy(p => p.TimestampUtc)
            .ThenBy(p => p.Label, StringComparer.Ordinal)
            .ToArray();

        var headers = new[] { "label", "ts", "thickness", "note" };
        var rows = ordered
            .Select(it => (IReadOnlyList<string>)new[]
            {
                it.Label,
                it.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
                it.Thickness.ToString("0.###", CultureInfo.InvariantCulture),
                it.Note ?? string.Empty
            })
            .ToArray();

        return (headers, rows);
    }

    private static List<Dictionary<string, object?>> ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Ожидается JSON-массив точек.");

        var list = new List<Dictionary<string, object?>>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var label = el.TryGetProperty("label", out var lbl) ? lbl.GetString() : null;
            var ts = el.TryGetProperty("ts", out var tsEl) ? tsEl.GetString() : null;
            var thk = el.TryGetProperty("thickness", out var thkEl) ? thkEl.ToString() : null;
            var note = el.TryGetProperty("note", out var noteEl) ? (noteEl.ValueKind == JsonValueKind.Null ? null : noteEl.ToString()) : null;

            if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("В точке отсутствует label.");
            if (string.IsNullOrWhiteSpace(ts)) throw new InvalidOperationException("В точке отсутствует ts.");
            if (string.IsNullOrWhiteSpace(thk)) throw new InvalidOperationException("В точке отсутствует thickness.");

            var dtoTs = DateTimeOffset.Parse(ts, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
            var dtoThk = decimal.Parse(thk, NumberStyles.Float, CultureInfo.InvariantCulture);

            list.Add(new Dictionary<string, object?>
            {
                ["label"] = label.Trim(),
                ["ts"] = dtoTs.ToString("O", CultureInfo.InvariantCulture),
                ["thickness"] = dtoThk,
                ["note"] = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
            });
        }

        return list;
    }

    private static List<Dictionary<string, object?>> ParseCsv(string csv)
    {
        var lines = csv.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var list = new List<Dictionary<string, object?>>();

        var startIndex = 0;
        if (lines.Length > 0)
        {
            var header = lines[0].Trim();
            if (header.StartsWith("label", StringComparison.OrdinalIgnoreCase))
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

            var label = parts[0]?.Trim();
            var ts = parts[1]?.Trim();
            var thk = parts[2]?.Trim();
            var note = parts.Count >= 4 ? parts[3]?.Trim() : null;

            if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException($"CSV: пустой label в строке {i + 1}.");
            if (string.IsNullOrWhiteSpace(ts)) throw new InvalidOperationException($"CSV: пустой ts в строке {i + 1}.");
            if (string.IsNullOrWhiteSpace(thk)) throw new InvalidOperationException($"CSV: пустой thickness в строке {i + 1}.");

            var dtoTs = DateTimeOffset.Parse(ts, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();
            var dtoThk = decimal.Parse(thk, NumberStyles.Float, CultureInfo.InvariantCulture);

            list.Add(new Dictionary<string, object?>
            {
                ["label"] = label,
                ["ts"] = dtoTs.ToString("O", CultureInfo.InvariantCulture),
                ["thickness"] = dtoThk,
                ["note"] = string.IsNullOrWhiteSpace(note) ? null : note
            });
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
            DisplayItems.Add(new PlantMeasurementHistoryGroupHeaderViewModel(g.Key));
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
            error = "Неверный формат даты. Используйте ISO, например: 2025-12-17T16:00:00Z";
            return false;
        }

        utcDateTime = dto.UtcDateTime;
        return true;
    }

    private static MeasurementSortOption[] BuildSortOptions() =>
        new[]
        {
            new MeasurementSortOption("ts_desc", "Дата (новые)", "m.ts desc, m.id desc"),
            new MeasurementSortOption("ts_asc", "Дата (старые)", "m.ts asc, m.id asc"),
            new MeasurementSortOption("thk_desc", "Толщина (убывание)", "m.thickness desc, m.ts desc, m.id desc"),
            new MeasurementSortOption("thk_asc", "Толщина (возрастание)", "m.thickness asc, m.ts desc, m.id desc"),
            new MeasurementSortOption("label", "Метка", "mp.label asc, m.ts desc, m.id desc")
        };
}

public sealed record PlantMeasurementHistoryItemViewModel(long Id, string Label, DateTimeOffset TimestampUtc, decimal Thickness, string? Note)
{
    public string TimestampLocal => TimestampUtc.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
}

public sealed record PlantMeasurementHistoryGroupHeaderViewModel(DateTime Day)
{
    public string Title => Day.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
}

public sealed record MeasurementSortOption(string Code, string Title, string OrderBySql)
{
    public override string ToString() => Title;
}
