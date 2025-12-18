using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npgsql;
using NpgsqlTypes;
using OilErp.Bootstrap;
using OilErp.Ui.Services;
using OilErp.Ui.Views;

namespace OilErp.Ui.ViewModels;

public sealed partial class CentralMeasurementsTabViewModel : ObservableObject
{
    private const int MaxColumns = 12;
    private const string CentralPlantCode = "CENTRAL";
    private static readonly EquipmentSortOption[] EquipmentSortOptionsSource =
    {
        new EquipmentSortOption("code", "Код (A→Z)", "asset_code"),
        new EquipmentSortOption("plant", "Завод (A→Z)", "plant_code nulls last, asset_code"),
        new EquipmentSortOption("last_measured_desc", "Последний замер (новые)", "last_ts desc nulls last, asset_code"),
        new EquipmentSortOption("last_measured_asc", "Последний замер (старые)", "last_ts asc nulls last, asset_code")
    };

    private static readonly EquipmentGroupOption[] EquipmentGroupOptionsSource =
    {
        new EquipmentGroupOption("none", "Без группировки"),
        new EquipmentGroupOption("plant", "Группировать: завод"),
        new EquipmentGroupOption("type", "Группировать: тип"),
        new EquipmentGroupOption("last_day", "Группировать: дата (последний замер)")
    };

    private readonly string connectionString;
    private bool? hasExtendedColumns;
    private readonly DispatcherTimer filterDebounceTimer;
    private bool filterRefreshPending;

    public CentralMeasurementsTabViewModel(string connectionString)
    {
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        Columns = new ObservableCollection<CentralMeasurementColumnViewModel>();
        Rows = new ObservableCollection<CentralMeasurementEquipmentRowViewModel>();
        DisplayRows = new ObservableCollection<object>();
        statusMessage = "Нажмите «Обновить» для загрузки таблицы.";
        EquipmentSortOptions = EquipmentSortOptionsSource;
        selectedEquipmentSort = EquipmentSortOptions[0];
        EquipmentGroupOptions = EquipmentGroupOptionsSource;
        selectedEquipmentGroup = EquipmentGroupOptions[0];

        filterDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        filterDebounceTimer.Tick += async (_, _) =>
        {
            filterDebounceTimer.Stop();
            if (IsBusy || !filterRefreshPending) return;
            filterRefreshPending = false;
            await RefreshAsync();
        };
    }

    public ObservableCollection<CentralMeasurementColumnViewModel> Columns { get; }

    public ObservableCollection<CentralMeasurementEquipmentRowViewModel> Rows { get; }

    public ObservableCollection<object> DisplayRows { get; }

    public IReadOnlyList<EquipmentSortOption> EquipmentSortOptions { get; }

    public IReadOnlyList<EquipmentGroupOption> EquipmentGroupOptions { get; }

    [ObservableProperty] private object? selectedDisplayRow;

    [ObservableProperty] private CentralMeasurementEquipmentRowViewModel? selectedRow;

    [ObservableProperty] private string filterText = string.Empty;

    [ObservableProperty] private EquipmentSortOption selectedEquipmentSort;

    [ObservableProperty] private EquipmentGroupOption selectedEquipmentGroup;

    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private string statusMessage;

    partial void OnSelectedDisplayRowChanged(object? value)
    {
        SelectedRow = value as CentralMeasurementEquipmentRowViewModel;
    }

    partial void OnIsBusyChanged(bool value)
    {
        AddMeasurementCommand.NotifyCanExecuteChanged();
        ShowHistoryCommand.NotifyCanExecuteChanged();
        OpenTransferCommand.NotifyCanExecuteChanged();

        if (!value && filterRefreshPending)
        {
            filterDebounceTimer.Stop();
            filterDebounceTimer.Start();
        }
    }

    partial void OnSelectedRowChanged(CentralMeasurementEquipmentRowViewModel? value)
    {
        AddMeasurementCommand.NotifyCanExecuteChanged();
        ShowHistoryCommand.NotifyCanExecuteChanged();
    }

    partial void OnFilterTextChanged(string value)
    {
        filterRefreshPending = true;
        filterDebounceTimer.Stop();
        filterDebounceTimer.Start();
    }

    partial void OnSelectedEquipmentSortChanged(EquipmentSortOption value)
    {
        RebuildDisplayRows();
    }

    partial void OnSelectedEquipmentGroupChanged(EquipmentGroupOption value)
    {
        RebuildDisplayRows();
    }

    private bool CanAddMeasurement() => !IsBusy && SelectedRow is not null && IsEditableInCentral(SelectedRow);
    private bool CanShowHistory() => !IsBusy && SelectedRow is not null;
    private bool CanOpenTransfer() => !IsBusy;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            AddMeasurementCommand.NotifyCanExecuteChanged();
            StatusMessage = "Загрузка оборудования и замеров (central, все заводы)...";

            SelectedDisplayRow = null;
            SelectedRow = null;
            Rows.Clear();
            Columns.Clear();
            DisplayRows.Clear();

            var rowsByCode = await LoadEquipmentAsync();
            await LoadCentralEventsAsync(rowsByCode);

            foreach (var row in Rows)
            {
                row.RebuildCells(Columns);
            }

            RebuildDisplayRows();
            StatusMessage = $"Оборудование: {Rows.Count}, столбцов замеров: {Columns.Count}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
            AppLogger.Error($"[ui] central measurements refresh error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            AddMeasurementCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddMeasurement))]
    public async Task AddMeasurementAsync()
    {
        var selected = SelectedRow;
        if (selected is null)
        {
            StatusMessage = "Выберите оборудование, чтобы добавить замер.";
            return;
        }
        if (!IsEditableInCentral(selected))
        {
            var plant = string.IsNullOrWhiteSpace(selected.PlantCode) ? "—" : selected.PlantCode;
            StatusMessage = $"Оборудование получено из {plant}. Добавление замеров доступно только на заводе.";
            return;
        }

        var assetCode = selected.Code.Trim();

        var vm = new CentralMeasurementEditWindowViewModel("Добавить замер", assetCode);
        var dialog = new CentralMeasurementEditWindow { DataContext = vm };
        var result = await UiDialogHost.ShowDialogAsync<CentralMeasurementEditResult?>(dialog);
        if (result is null) return;

        try
        {
            IsBusy = true;
            AddMeasurementCommand.NotifyCanExecuteChanged();

            var last = await LoadLastAnalyticsAsync(assetCode);
            var prevDate = last.LastDateUtc;
            var prevThk = last.LastThickness;

            if (prevThk is not null && (decimal)result.Thickness > prevThk.Value)
            {
                StatusMessage = $"Толщина не может увеличиваться (последняя={prevThk.Value:0.###}).";
                return;
            }

            var selectedLocalDate = result.DateLocal.Date;
            if (prevDate is not null)
            {
                var lastLocalDate = prevDate.Value.ToLocalTime().Date;
                if (selectedLocalDate < lastLocalDate)
                {
                    StatusMessage = $"Дата не может быть раньше последней ({lastLocalDate:dd.MM.yyyy}).";
                    return;
                }
            }

            var localMidnight = DateTime.SpecifyKind(selectedLocalDate, DateTimeKind.Local);
            var timestampUtc = localMidnight.ToUniversalTime();
            if (prevDate is not null && timestampUtc <= prevDate.Value)
            {
                timestampUtc = prevDate.Value.AddSeconds(1);
            }

            var lastThk = (decimal)Math.Round(result.Thickness, 3);
            await InsertBatchAsync(assetCode, prevThk, prevDate, lastThk, timestampUtc, result.Label, result.Note);

            await RefreshAsync();
            StatusMessage = "Замер добавлен.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка добавления: {ex.Message}";
            AppLogger.Error($"[ui] central measurement add error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            AddMeasurementCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanShowHistory))]
    public async Task ShowHistoryAsync()
    {
        var selected = SelectedRow;
        if (selected is null)
        {
            StatusMessage = "Выберите оборудование, чтобы открыть историю.";
            return;
        }

        var assetCode = selected.Code.Trim();
        var vm = new CentralMeasurementHistoryWindowViewModel(assetCode, connectionString);
        var dialog = new CentralMeasurementHistoryWindow { DataContext = vm };
        _ = vm.RefreshAsync();
        await UiDialogHost.ShowDialogAsync<bool?>(dialog);

        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanOpenTransfer))]
    public async Task OpenTransferAsync()
    {
        var vm = new CentralMeasurementsTransferWindowViewModel(connectionString);
        var dialog = new CentralMeasurementsTransferWindow { DataContext = vm };
        await UiDialogHost.ShowDialogAsync<bool?>(dialog);

        await RefreshAsync();
    }

    private async Task<Dictionary<string, CentralMeasurementEquipmentRowViewModel>> LoadEquipmentAsync()
    {
        var map = new Dictionary<string, CentralMeasurementEquipmentRowViewModel>(StringComparer.OrdinalIgnoreCase);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            with last_batch as (
              select distinct on (asset_code)
                asset_code,
                source_plant,
                last_date
              from public.measurement_batches
              order by asset_code, last_date desc, id desc
            )
            select
              coalesce(ag.asset_code, lb.asset_code) as asset_code,
              ag.name,
              ag.type,
              coalesce(ag.plant_code, lb.source_plant) as plant_code
            from public.assets_global ag
            full join last_batch lb on lb.asset_code = ag.asset_code
            where @q is null
               or coalesce(ag.asset_code, lb.asset_code) ilike @q
               or coalesce(ag.name,'') ilike @q
               or coalesce(ag.type,'') ilike @q
               or coalesce(ag.plant_code, lb.source_plant, '') ilike @q
            order by coalesce(ag.asset_code, lb.asset_code)
            limit 500
            """;
        cmd.Parameters.Add("q", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(FilterText) ? DBNull.Value : $"%{FilterText.Trim()}%";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var code = reader.GetString(0);
            var name = reader.IsDBNull(1) ? null : reader.GetString(1);
            var type = reader.IsDBNull(2) ? null : reader.GetString(2);
            var plant = reader.IsDBNull(3) ? null : reader.GetString(3);

            var row = new CentralMeasurementEquipmentRowViewModel(code, name, type, plant);
            Rows.Add(row);
            map[code] = row;
        }

        return map;
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        FilterText = string.Empty;
        await RefreshAsync();
    }

    private async Task LoadCentralEventsAsync(Dictionary<string, CentralMeasurementEquipmentRowViewModel> rowsByCode)
    {
        var columns = new List<DateTimeOffset>(MaxColumns);
        var columnsSet = new HashSet<DateTimeOffset>();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            select
              asset_code,
              last_date as ts,
              last_thk as thickness
            from public.measurement_batches
            order by last_date desc, id desc
            limit 1500
            """;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var code = reader.GetString(0);
            if (!rowsByCode.TryGetValue(code, out var row)) continue;

            var dt = reader.GetFieldValue<DateTime>(1);
            if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            var ts = NormalizeToLocalDay(new DateTimeOffset(dt));
            var thk = reader.GetFieldValue<decimal>(2);

            if (!columnsSet.Contains(ts))
            {
                if (columns.Count >= MaxColumns) continue;
                columns.Add(ts);
                columnsSet.Add(ts);
            }

            row.TryAddValue(ts, thk);
        }

        Columns.Clear();
        foreach (var ts in columns)
        {
            Columns.Add(new CentralMeasurementColumnViewModel(ts, FormatColumn(ts)));
        }
    }

    private void RebuildDisplayRows()
    {
        DisplayRows.Clear();
        if (Rows.Count == 0) return;

        var orderedRows = GetSortedRows();
        if (string.Equals(SelectedEquipmentGroup.Code, "none", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var row in orderedRows)
            {
                DisplayRows.Add(row);
            }

            return;
        }

        if (string.Equals(SelectedEquipmentGroup.Code, "plant", StringComparison.OrdinalIgnoreCase))
        {
            var groups = orderedRows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.PlantCode) ? "—" : r.PlantCode.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var g in groups)
            {
                DisplayRows.Add(new CentralMeasurementsGroupHeaderViewModel($"Завод: {g.Key}", g.Count()));
                foreach (var row in g)
                {
                    DisplayRows.Add(row);
                }
            }

            return;
        }

        if (string.Equals(SelectedEquipmentGroup.Code, "type", StringComparison.OrdinalIgnoreCase))
        {
            var groups = orderedRows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Type) ? "—" : r.Type.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var g in groups)
            {
                DisplayRows.Add(new CentralMeasurementsGroupHeaderViewModel($"Тип: {g.Key}", g.Count()));
                foreach (var row in g)
                {
                    DisplayRows.Add(row);
                }
            }

            return;
        }

        if (string.Equals(SelectedEquipmentGroup.Code, "last_day", StringComparison.OrdinalIgnoreCase))
        {
            var withMeasurements = orderedRows
                .Where(r => r.LastMeasurementUtc is not null)
                .GroupBy(r => r.LastMeasurementUtc!.Value.ToLocalTime().Date)
                .OrderByDescending(g => g.Key);

            foreach (var g in withMeasurements)
            {
                DisplayRows.Add(new CentralMeasurementsGroupHeaderViewModel(g.Key.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), g.Count()));
                foreach (var row in g)
                {
                    DisplayRows.Add(row);
                }
            }

            var without = orderedRows.Where(r => r.LastMeasurementUtc is null).ToList();
            if (without.Count > 0)
            {
                DisplayRows.Add(new CentralMeasurementsGroupHeaderViewModel("Нет замеров", without.Count));
                foreach (var row in without)
                {
                    DisplayRows.Add(row);
                }
            }
        }
    }

    private IReadOnlyList<CentralMeasurementEquipmentRowViewModel> GetSortedRows()
    {
        var comparer = StringComparer.OrdinalIgnoreCase;

        return SelectedEquipmentSort.Code switch
        {
            "code" => Rows.OrderBy(r => r.Code, comparer).ToList(),
            "plant" => Rows
                .OrderBy(r => string.IsNullOrWhiteSpace(r.PlantCode))
                .ThenBy(r => r.PlantCode, comparer)
                .ThenBy(r => r.Code, comparer)
                .ToList(),
            "last_measured_desc" => Rows
                .OrderByDescending(r => r.LastMeasurementUtc ?? DateTimeOffset.MinValue)
                .ThenBy(r => r.Code, comparer)
                .ToList(),
            "last_measured_asc" => Rows
                .OrderBy(r => r.LastMeasurementUtc ?? DateTimeOffset.MaxValue)
                .ThenBy(r => r.Code, comparer)
                .ToList(),
            _ => Rows.ToList()
        };
    }

    private static string FormatColumn(DateTimeOffset ts)
    {
        var local = ts.ToLocalTime();
        return local.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset NormalizeToLocalDay(DateTimeOffset ts)
    {
        var local = ts.ToLocalTime().Date;
        return new DateTimeOffset(local);
    }

    private async Task InsertBatchAsync(
        string assetCode,
        decimal? prevThk,
        DateTime? prevDateUtc,
        decimal lastThk,
        DateTime lastDateUtc,
        string label,
        string? note)
    {
        StatusMessage = "Сохраняем замер (central)...";
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        var hasExtras = await HasExtendedColumnsAsync(conn);
        await using var cmd = conn.CreateCommand();
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

        cmd.Parameters.Add("plant", NpgsqlDbType.Text).Value = CentralPlantCode;
        cmd.Parameters.Add("asset", NpgsqlDbType.Text).Value = assetCode.Trim();
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

    private async Task<AnalyticsLastState> LoadLastAnalyticsAsync(string assetCode)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            select last_date, last_thk
            from public.analytics_cr
            where lower(asset_code) = lower(@code)
            limit 1
            """;
        cmd.Parameters.AddWithValue("@code", assetCode.Trim());

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return new AnalyticsLastState(null, null);
        }

        DateTime? lastDate = null;
        if (!reader.IsDBNull(0))
        {
            var dt = reader.GetFieldValue<DateTime>(0);
            if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            lastDate = dt;
        }

        var lastThk = reader.IsDBNull(1) ? (decimal?)null : reader.GetFieldValue<decimal>(1);
        return new AnalyticsLastState(lastDate, lastThk);
    }

    private sealed record AnalyticsLastState(DateTime? LastDateUtc, decimal? LastThickness);

    private static bool IsEditableInCentral(CentralMeasurementEquipmentRowViewModel row)
    {
        if (row is null) return false;
        return string.IsNullOrWhiteSpace(row.PlantCode)
               || string.Equals(row.PlantCode, CentralPlantCode, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record CentralMeasurementColumnViewModel(DateTimeOffset Timestamp, string Header);

public sealed partial class CentralMeasurementEquipmentRowViewModel : ObservableObject
{
    private readonly Dictionary<DateTimeOffset, decimal> valuesByTimestamp = new();

    public CentralMeasurementEquipmentRowViewModel(string code, string? name, string? type, string? plantCode)
    {
        Code = code;
        Name = name;
        Type = type;
        PlantCode = NormalizePlant(plantCode);
        Cells = new ObservableCollection<string>();
    }

    public string Code { get; }

    public string? Name { get; }

    public string? Type { get; }

    public string? PlantCode { get; }

    public DateTimeOffset? LastMeasurementUtc { get; private set; }

    public ObservableCollection<string> Cells { get; }

    public void TryAddValue(DateTimeOffset ts, decimal thickness)
    {
        if (!valuesByTimestamp.ContainsKey(ts))
        {
            valuesByTimestamp[ts] = thickness;
        }

        if (LastMeasurementUtc is null || ts > LastMeasurementUtc.Value)
        {
            LastMeasurementUtc = ts;
        }
    }

    public void RebuildCells(IReadOnlyList<CentralMeasurementColumnViewModel> columns)
    {
        Cells.Clear();
        foreach (var col in columns)
        {
            if (valuesByTimestamp.TryGetValue(col.Timestamp, out var thk))
            {
                Cells.Add(thk.ToString("0.###", CultureInfo.InvariantCulture));
            }
            else
            {
                Cells.Add("—");
            }
        }
    }

    private static string? NormalizePlant(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var upper = value.Trim().ToUpperInvariant();
        return upper == "KRNPZ" ? "KNPZ" : upper;
    }
}

public sealed record CentralMeasurementsGroupHeaderViewModel(string Title, int Count)
{
    public string DisplayTitle => $"{Title} ({Count})";
}
