using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npgsql;
using NpgsqlTypes;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Util;
using OilErp.Ui.Services;
using OilErp.Ui.Views;
using AnpzInsertService = OilErp.Core.Services.Plants.ANPZ.SpInsertMeasurementBatchService;
using KrnpzInsertService = OilErp.Core.Services.Plants.KRNPZ.SpInsertMeasurementBatchService;
using System.Text.RegularExpressions;

namespace OilErp.Ui.ViewModels;

public sealed partial class PlantMeasurementsTabViewModel : ObservableObject
{
    private const int MaxColumns = 36;
    private static readonly EquipmentSortOption[] EquipmentSortOptionsSource =
    {
        new EquipmentSortOption("created_desc", "Создано (новые)", "created_at desc, asset_code"),
        new EquipmentSortOption("code", "Код (A→Z)", "asset_code"),
        new EquipmentSortOption("location", "Локация (A→Z)", "location nulls last, asset_code"),
        new EquipmentSortOption("status", "Статус (A→Z)", "status nulls last, asset_code"),
        new EquipmentSortOption("last_measured_desc", "Последний замер (новые)", "last_ts desc nulls last, asset_code"),
        new EquipmentSortOption("last_measured_asc", "Последний замер (старые)", "last_ts asc nulls last, asset_code")
    };

    private static readonly EquipmentGroupOption[] EquipmentGroupOptionsSource =
    {
        new EquipmentGroupOption("none", "Без группировки"),
        new EquipmentGroupOption("status", "Группировать: статус"),
        new EquipmentGroupOption("location", "Группировать: локация"),
        new EquipmentGroupOption("last_day", "Группировать: дата (последний замер)"),
        new EquipmentGroupOption("code_prefix", "Группировать: код (префикс)")
    };
    private static readonly ColumnSortOption[] ColumnSortOptionsSource =
    {
        new ColumnSortOption("date_asc", "Даты: старые → новые", false),
        new ColumnSortOption("date_desc", "Даты: новые → старые", true)
    };

    private readonly DatabaseProfile profile;
    private readonly IStoragePort storage;
    private readonly string connectionString;
    private readonly DispatcherTimer filterDebounceTimer;
    private bool filterRefreshPending;
    private List<(DateTimeOffset Date, string Label)> columnKeys = new();

    public PlantMeasurementsTabViewModel(DatabaseProfile profile, IStoragePort storage, string connectionString)
    {
        if (profile is not (DatabaseProfile.PlantAnpz or DatabaseProfile.PlantKrnpz))
            throw new ArgumentOutOfRangeException(nameof(profile), profile, "Plant profile expected");

        this.profile = profile;
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

        PlantCode = profile == DatabaseProfile.PlantKrnpz ? "KNPZ" : "ANPZ";
        Columns = new ObservableCollection<PlantMeasurementColumnViewModel>();
        ColumnGroups = new ObservableCollection<MeasurementDateGroupHeaderViewModel>();
        Rows = new ObservableCollection<PlantMeasurementEquipmentRowViewModel>();
        DisplayRows = new ObservableCollection<object>();
        statusMessage = "Нажмите «Обновить» для загрузки таблицы.";
        EquipmentSortOptions = EquipmentSortOptionsSource;
        selectedEquipmentSort = EquipmentSortOptions[0];
        EquipmentGroupOptions = EquipmentGroupOptionsSource;
        selectedEquipmentGroup = EquipmentGroupOptions[0];
        ColumnSortOptions = ColumnSortOptionsSource;
        selectedColumnSort = ColumnSortOptions[0];

        filterDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        filterDebounceTimer.Tick += async (_, _) =>
        {
            filterDebounceTimer.Stop();
            if (IsBusy || !filterRefreshPending) return;
            filterRefreshPending = false;
            await RefreshAsync();
        };
    }

    public string PlantCode { get; }

    public string PlantCodeDisplay => PlantCode switch
    {
        "ANPZ" => "АНПЗ",
        "KNPZ" => "КНПЗ",
        _ => PlantCode
    };

    public ObservableCollection<PlantMeasurementColumnViewModel> Columns { get; }

    public ObservableCollection<MeasurementDateGroupHeaderViewModel> ColumnGroups { get; }

    public ObservableCollection<PlantMeasurementEquipmentRowViewModel> Rows { get; }

    public ObservableCollection<object> DisplayRows { get; }

    public IReadOnlyList<EquipmentSortOption> EquipmentSortOptions { get; }

    public IReadOnlyList<EquipmentGroupOption> EquipmentGroupOptions { get; }

    public IReadOnlyList<ColumnSortOption> ColumnSortOptions { get; }

    [ObservableProperty] private object? selectedDisplayRow;

    [ObservableProperty] private PlantMeasurementEquipmentRowViewModel? selectedRow;

    partial void OnSelectedDisplayRowChanged(object? value)
    {
        SelectedRow = value as PlantMeasurementEquipmentRowViewModel;
    }

    partial void OnSelectedRowChanged(PlantMeasurementEquipmentRowViewModel? value)
    {
        AddMeasurementCommand.NotifyCanExecuteChanged();
        EditLastMeasurementCommand.NotifyCanExecuteChanged();
        DeleteLastMeasurementCommand.NotifyCanExecuteChanged();
        OpenTransferCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty] private string filterText = string.Empty;

    [ObservableProperty] private EquipmentSortOption selectedEquipmentSort;

    [ObservableProperty] private EquipmentGroupOption selectedEquipmentGroup;

    [ObservableProperty] private ColumnSortOption selectedColumnSort;

    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private string statusMessage;

    partial void OnSelectedEquipmentSortChanged(EquipmentSortOption value)
    {
        RebuildDisplayRows();
    }

    partial void OnSelectedEquipmentGroupChanged(EquipmentGroupOption value)
    {
        RebuildDisplayRows();
    }

    partial void OnSelectedColumnSortChanged(ColumnSortOption value)
    {
        ApplyColumnOrder();
    }

    partial void OnIsBusyChanged(bool value)
    {
        AddMeasurementCommand.NotifyCanExecuteChanged();
        EditLastMeasurementCommand.NotifyCanExecuteChanged();
        DeleteLastMeasurementCommand.NotifyCanExecuteChanged();
        OpenTransferCommand.NotifyCanExecuteChanged();

        if (!value && filterRefreshPending)
        {
            filterDebounceTimer.Stop();
            filterDebounceTimer.Start();
        }
    }

    partial void OnFilterTextChanged(string value)
    {
        filterRefreshPending = true;
        filterDebounceTimer.Stop();
        filterDebounceTimer.Start();
    }

    private bool CanAddMeasurement() => !IsBusy && SelectedRow is not null;
    private bool CanEditOrDeleteMeasurement() => !IsBusy && SelectedRow is not null;
    private bool CanOpenTransfer() => !IsBusy;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            AddMeasurementCommand.NotifyCanExecuteChanged();
            StatusMessage = "Загрузка оборудования и замеров...";

            SelectedDisplayRow = null;
            SelectedRow = null;
            Rows.Clear();
            Columns.Clear();
            ColumnGroups.Clear();
            DisplayRows.Clear();

            var rowsByCode = await LoadEquipmentAsync();
            await LoadMeasurementsAsync(rowsByCode);

            RebuildDisplayRows();
            StatusMessage = $"Оборудование: {Rows.Count}, столбцов замеров: {Columns.Count}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
            AppLogger.Error($"[ui] plant measurements refresh error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            AddMeasurementCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    public async Task ClearFilterAsync()
    {
        FilterText = string.Empty;
        await RefreshAsync();
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

        var assetCode = selected.Code.Trim();
        var baseTimestampUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        var nextLabel = await GenerateNextLabelAsync(assetCode);
        var vm = new PlantMeasurementEditWindowViewModel(
            "Добавить замер",
            PlantCode,
            assetCode,
            nextLabel,
            isLabelReadOnly: true);

        var dialog = new PlantMeasurementEditWindow { DataContext = vm };
        var result = await UiDialogHost.ShowDialogAsync<PlantMeasurementEditResult?>(dialog);
        if (result is null) return;

        try
        {
            IsBusy = true;
            AddMeasurementCommand.NotifyCanExecuteChanged();
            StatusMessage = "Сохраняем замер...";

            var timestampUtc = baseTimestampUtc;
            var inserted = 0;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var point = new MeasurementPointDto(
                    result.Label,
                    timestampUtc,
                    (decimal)Math.Round(result.Thickness, 3),
                    result.Note);

                var pointsJson = MeasurementBatchPayloadBuilder.BuildJson(point);

                await using var tx = await storage.BeginTransactionAsync(CancellationToken.None);
                try
                {
                    inserted = await ExecuteInsertAsync(assetCode, pointsJson, CancellationToken.None);
                    await tx.CommitAsync(CancellationToken.None);
                    break;
                }
                catch (Exception ex) when (ShouldBumpTimestamp(ex) && attempt < 19)
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    timestampUtc = timestampUtc.AddSeconds(1);
                    continue;
                }
            }

            ApplyMeasurementToMatrix(selected, new DateTimeOffset(timestampUtc), result.Label, (decimal)Math.Round(result.Thickness, 3));
            RebuildDisplayRows();
            StatusMessage = $"Сохранено (строк={inserted}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сохранения: {ex.Message}";
            AppLogger.Error($"[ui] plant measurement insert error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            AddMeasurementCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenTransfer))]
    public async Task OpenTransferAsync()
    {
        var vm = new PlantMeasurementsTransferWindowViewModel(PlantCode, connectionString);
        var dialog = new PlantMeasurementsTransferWindow { DataContext = vm };
        await UiDialogHost.ShowDialogAsync<bool?>(dialog);

        await RefreshAsync();
    }
    [RelayCommand(CanExecute = nameof(CanEditOrDeleteMeasurement))]
    public async Task EditLastMeasurementAsync()
    {
        var selected = SelectedRow;
        if (selected is null)
        {
            StatusMessage = "Выберите оборудование, чтобы изменить замер.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Загрузка последнего замера...";

            var assetCode = selected.Code.Trim();
            var latest = await LoadLatestMeasurementAsync(assetCode);
            if (latest is null)
            {
                StatusMessage = "Для выбранного оборудования нет замеров.";
                return;
            }

            var vm = new PlantMeasurementEditWindowViewModel(
                "Изменить последний замер",
                PlantCode,
                assetCode,
                latest.Label,
                initialThickness: (double)latest.Thickness,
                initialNote: latest.Note,
                isLabelReadOnly: true);

            var dialog = new PlantMeasurementEditWindow { DataContext = vm };
            var result = await UiDialogHost.ShowDialogAsync<PlantMeasurementEditResult?>(dialog);
            if (result is null)
            {
                StatusMessage = "Отменено.";
                return;
            }

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            await UpdateMeasurementAsync(conn, tx, latest.Id, result.Thickness, result.Note);
            await EnqueueBatchToCentralAsync(conn, tx, assetCode);

            await tx.CommitAsync();
            await RefreshAsync();
            StatusMessage = "Замер обновлён.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка изменения: {ex.Message}";
            AppLogger.Error($"[ui] plant measurement edit error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditOrDeleteMeasurement))]
    public async Task DeleteLastMeasurementAsync()
    {
        var selected = SelectedRow;
        if (selected is null)
        {
            StatusMessage = "Выберите оборудование, чтобы удалить замер.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Загрузка последнего замера...";

            var assetCode = selected.Code.Trim();
            var latest = await LoadLatestMeasurementAsync(assetCode);
            if (latest is null)
            {
                StatusMessage = "Для выбранного оборудования нет замеров.";
                return;
            }

            var confirmVm = new ConfirmDialogViewModel(
                "Удалить замер",
                $"Удалить последний замер для {assetCode}?\n\n" +
                $"метка: {latest.Label}\n" +
                $"дата: {latest.TimestampUtc:O}\n" +
                $"толщина: {latest.Thickness:0.###}",
                confirmText: "Удалить",
                cancelText: "Отмена");

            var confirmDialog = new ConfirmDialogWindow { DataContext = confirmVm };
            var confirm = await UiDialogHost.ShowDialogAsync<bool?>(confirmDialog);
            if (confirm != true)
            {
                StatusMessage = "Отменено.";
                return;
            }

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            await DeleteMeasurementAsync(conn, tx, latest.Id);
            await EnqueueBatchToCentralAsync(conn, tx, assetCode);

            await tx.CommitAsync();
            await RefreshAsync();
            StatusMessage = "Замер удалён.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка удаления: {ex.Message}";
            AppLogger.Error($"[ui] plant measurement delete error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }


    private static bool ShouldBumpTimestamp(Exception ex)
    {
        Exception? current = ex;
        while (current is not null)
        {
            var msg = current.Message ?? string.Empty;
            if (msg.Contains("incoming measurements must be newer", StringComparison.OrdinalIgnoreCase))
                return true;
            if (msg.Contains("points must be strictly increasing", StringComparison.OrdinalIgnoreCase))
                return true;

            current = current.InnerException;
        }
        return false;
    }

    private async Task<Dictionary<string, PlantMeasurementEquipmentRowViewModel>> LoadEquipmentAsync()
    {
        var map = new Dictionary<string, PlantMeasurementEquipmentRowViewModel>(StringComparer.OrdinalIgnoreCase);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
                           select a.asset_code, a.location, a.status, a.created_at, lm.ts as last_ts
                           from public.assets_local a
                           left join lateral (
                             select m.ts
                             from public.measurements m
                             join public.measurement_points mp on mp.id = m.point_id
                             where mp.asset_id = a.id
                             order by m.ts desc, m.id desc
                             limit 1
                           ) lm on true
                           where @q is null
                              or a.asset_code ilike @q
                              or coalesce(location,'') ilike @q
                              or coalesce(status,'') ilike @q
                           order by {SelectedEquipmentSort.OrderBySql}
                           limit 300
                           """;
        cmd.Parameters.Add("q", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(FilterText) ? DBNull.Value : $"%{FilterText.Trim()}%";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var code = reader.GetString(0);
            var location = reader.IsDBNull(1) ? null : reader.GetString(1);
            var status = NormalizeStatus(reader.IsDBNull(2) ? null : reader.GetString(2));

            DateTimeOffset? createdAt = null;
            if (!reader.IsDBNull(3))
            {
                var dt = reader.GetFieldValue<DateTime>(3);
                if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                createdAt = new DateTimeOffset(dt).ToUniversalTime();
            }

            DateTimeOffset? lastMeasuredAt = null;
            if (!reader.IsDBNull(4))
            {
                var dt = reader.GetFieldValue<DateTime>(4);
                if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                lastMeasuredAt = new DateTimeOffset(dt).ToUniversalTime();
            }

            var row = new PlantMeasurementEquipmentRowViewModel(code, location, status, createdAt, lastMeasuredAt);
            Rows.Add(row);
            map[code] = row;
        }

        return map;
    }

    private async Task LoadMeasurementsAsync(Dictionary<string, PlantMeasurementEquipmentRowViewModel> rowsByCode)
    {
        if (rowsByCode.Count == 0) return;

        var columns = new List<(DateTimeOffset Date, string Label)>(MaxColumns);
        var columnsSet = new HashSet<(DateTimeOffset Date, string Label)>();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            select a.asset_code, m.ts, m.thickness, mp.label
            from public.measurements m
            join public.measurement_points mp on mp.id = m.point_id
            join public.assets_local a on a.id = mp.asset_id
            order by m.ts desc, m.id desc
            limit 1500
            """;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var code = reader.GetString(0);
            if (!rowsByCode.TryGetValue(code, out var row)) continue;

            var tsValue = reader.GetFieldValue<DateTime>(1);
            if (tsValue.Kind == DateTimeKind.Unspecified) tsValue = DateTime.SpecifyKind(tsValue, DateTimeKind.Utc);
            var ts = NormalizeToLocalDay(new DateTimeOffset(tsValue));
            var thk = reader.GetFieldValue<decimal>(2);
            var label = NormalizeLabel(reader.IsDBNull(3) ? null : reader.GetString(3));

            var key = (ts, label);
            if (!columnsSet.Contains(key))
            {
                if (columns.Count >= MaxColumns)
                {
                    continue;
                }

                columns.Add(key);
                columnsSet.Add(key);
            }

            row.TryAddValue(ts, label, thk);
        }

        columnKeys = columns;
        ApplyColumnOrder();
    }

    private void ApplyMeasurementToMatrix(PlantMeasurementEquipmentRowViewModel row, DateTimeOffset ts, string label, decimal thickness)
    {
        var normalized = NormalizeToLocalDay(ts);
        var normalizedLabel = NormalizeLabel(label);
        EnsureColumn(normalized, normalizedLabel);
        row.TryAddValue(normalized, normalizedLabel, thickness);

        foreach (var r in Rows)
        {
            r.RebuildCells(Columns);
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

        if (string.Equals(SelectedEquipmentGroup.Code, "status", StringComparison.OrdinalIgnoreCase))
        {
            var groups = orderedRows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Status) ? "—" : r.Status.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var g in groups)
            {
                DisplayRows.Add(new PlantMeasurementsGroupHeaderViewModel($"Статус: {g.Key}", g.Count()));
                foreach (var row in g)
                {
                    DisplayRows.Add(row);
                }
            }

            return;
        }

        if (string.Equals(SelectedEquipmentGroup.Code, "location", StringComparison.OrdinalIgnoreCase))
        {
            var groups = orderedRows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Location) ? "—" : r.Location.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var g in groups)
            {
                DisplayRows.Add(new PlantMeasurementsGroupHeaderViewModel($"Локация: {g.Key}", g.Count()));
                foreach (var row in g)
                {
                    DisplayRows.Add(row);
                }
            }

            return;
        }

        if (string.Equals(SelectedEquipmentGroup.Code, "code_prefix", StringComparison.OrdinalIgnoreCase))
        {
            var groups = orderedRows
                .GroupBy(r => GetCodePrefix(r.Code), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var g in groups)
            {
                DisplayRows.Add(new PlantMeasurementsGroupHeaderViewModel($"Код: {g.Key}", g.Count()));
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
                DisplayRows.Add(new PlantMeasurementsGroupHeaderViewModel(g.Key.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), g.Count()));
                foreach (var row in g)
                {
                    DisplayRows.Add(row);
                }
            }

            var withoutMeasurements = orderedRows.Where(r => r.LastMeasurementUtc is null).ToList();
            if (withoutMeasurements.Count > 0)
            {
                DisplayRows.Add(new PlantMeasurementsGroupHeaderViewModel("Нет замеров", withoutMeasurements.Count));
                foreach (var row in withoutMeasurements)
                {
                    DisplayRows.Add(row);
                }
            }
        }
    }

    private IReadOnlyList<PlantMeasurementEquipmentRowViewModel> GetSortedRows()
    {
        var comparer = StringComparer.OrdinalIgnoreCase;

        return SelectedEquipmentSort.Code switch
        {
            "created_desc" => Rows
                .OrderByDescending(r => r.CreatedAt ?? DateTimeOffset.MinValue)
                .ThenBy(r => r.Code, comparer)
                .ToList(),
            "code" => Rows
                .OrderBy(r => r.Code, comparer)
                .ToList(),
            "location" => Rows
                .OrderBy(r => string.IsNullOrWhiteSpace(r.Location))
                .ThenBy(r => r.Location, comparer)
                .ThenBy(r => r.Code, comparer)
                .ToList(),
            "status" => Rows
                .OrderBy(r => string.IsNullOrWhiteSpace(r.Status))
                .ThenBy(r => r.Status, comparer)
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

    private static string GetCodePrefix(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "—";

        var value = code.Trim();
        var dash = value.IndexOf('-', StringComparison.Ordinal);
        if (dash > 0) return value[..dash];

        var underscore = value.IndexOf('_', StringComparison.Ordinal);
        if (underscore > 0) return value[..underscore];

        return value.Length > 2 ? value[..2] : value;
    }

    private void EnsureColumn(DateTimeOffset ts, string label)
    {
        ts = NormalizeToLocalDay(ts);
        var key = (ts, label);
        if (columnKeys.Contains(key)) return;

        columnKeys.Add(key);
        if (columnKeys.Count > MaxColumns)
        {
            var oldest = columnKeys
                .OrderBy(k => k.Date)
                .ThenBy(k => GetLabelOrder(k.Label))
                .ThenBy(k => k.Label, StringComparer.OrdinalIgnoreCase)
                .First();
            columnKeys.Remove(oldest);
        }

        ApplyColumnOrder();
    }

    private void ApplyColumnOrder()
    {
        Columns.Clear();
        ColumnGroups.Clear();
        if (columnKeys.Count == 0) return;

        var grouped = columnKeys
            .GroupBy(k => k.Date)
            .Select(g => new
            {
                Date = g.Key,
                Labels = g.Select(x => x.Label).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            })
            .ToList();

        var orderedDates = SelectedColumnSort.Descending
            ? grouped.OrderByDescending(g => g.Date)
            : grouped.OrderBy(g => g.Date);

        var totalColumns = 0;
        foreach (var group in orderedDates)
        {
            var labels = group.Labels
                .OrderBy(GetLabelOrder)
                .ThenBy(l => l, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (totalColumns + labels.Count > MaxColumns)
            {
                labels = labels.Take(Math.Max(0, MaxColumns - totalColumns)).ToList();
            }

            if (labels.Count == 0) continue;

            ColumnGroups.Add(new MeasurementDateGroupHeaderViewModel(
                group.Date.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                labels.Count));

            foreach (var label in labels)
            {
                Columns.Add(new PlantMeasurementColumnViewModel(group.Date, label, label));
            }

            totalColumns += labels.Count;
            if (totalColumns >= MaxColumns) break;
        }

        foreach (var row in Rows)
        {
            row.RebuildCells(Columns);
        }
    }

    private static DateTimeOffset NormalizeToLocalDay(DateTimeOffset ts)
    {
        var local = ts.ToLocalTime().Date;
        return new DateTimeOffset(local);
    }

    private Task<int> ExecuteInsertAsync(string equipmentCodeValue, string pointsJson, CancellationToken ct)
    {
        if (profile == DatabaseProfile.PlantKrnpz)
        {
            var krnpz = new KrnpzInsertService(storage);
            return krnpz.sp_insert_measurement_batchAsync(equipmentCodeValue, pointsJson, PlantCode, ct);
        }

        var anpz = new AnpzInsertService(storage);
        return anpz.sp_insert_measurement_batchAsync(equipmentCodeValue, pointsJson, PlantCode, ct);
    }

    private sealed record MeasurementRow(long Id, string Label, DateTimeOffset TimestampUtc, decimal Thickness, string? Note);

    private async Task<MeasurementRow?> LoadLatestMeasurementAsync(string assetCode)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          select m.id, mp.label, m.ts, m.thickness, m.note
                          from public.measurements m
                          join public.measurement_points mp on mp.id = m.point_id
                          join public.assets_local a on a.id = mp.asset_id
                          where a.asset_code = @code
                          order by m.ts desc, m.id desc
                          limit 1
                          """;
        cmd.Parameters.AddWithValue("code", assetCode);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var id = reader.GetInt64(0);
        var label = reader.GetString(1);

        var tsValue = reader.GetFieldValue<DateTime>(2);
        if (tsValue.Kind == DateTimeKind.Unspecified) tsValue = DateTime.SpecifyKind(tsValue, DateTimeKind.Utc);
        var ts = new DateTimeOffset(tsValue).ToUniversalTime();

        var thickness = reader.GetFieldValue<decimal>(3);
        var note = reader.IsDBNull(4) ? null : reader.GetString(4);

        return new MeasurementRow(id, label, ts, thickness, note);
    }

    private static readonly Regex LabelRegex = new(@"^T(?<n>\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private async Task<string> GenerateNextLabelAsync(string assetCode)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          select mp.label
                          from public.measurement_points mp
                          join public.assets_local a on a.id = mp.asset_id
                          where a.asset_code = @code
                          order by mp.id
                          """;
        cmd.Parameters.AddWithValue("code", assetCode);

        var max = 0;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.IsDBNull(0)) continue;
            var lbl = reader.GetString(0);
            var match = LabelRegex.Match(lbl.Trim());
            if (match.Success && int.TryParse(match.Groups["n"].Value, out var num))
            {
                if (num > max) max = num;
            }
        }

        var next = Math.Max(1, max + 1);
        return $"T{next}";
    }

    private static string NormalizeStatus(string? value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? "Норма" : value.Trim();
        return trimmed.ToUpperInvariant() switch
        {
            "OK" => "Норма",
            "WARNING" => "Предупреждение",
            "CRITICAL" => "Критично",
            "UNKNOWN" => "Неизвестно",
            _ => trimmed
        };
    }

    private static string NormalizeLabel(string? label)
    {
        var trimmed = string.IsNullOrWhiteSpace(label) ? "—" : label.Trim();
        return trimmed.ToUpperInvariant();
    }

    private static int GetLabelOrder(string label)
    {
        if (string.IsNullOrWhiteSpace(label) || label == "—") return int.MaxValue;
        var match = LabelRegex.Match(label.Trim());
        return match.Success && int.TryParse(match.Groups["n"].Value, out var num) ? num : int.MaxValue;
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

    private async Task EnqueueBatchToCentralAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string assetCode)
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
            StatusMessage = "Замеры обновлены локально, но для центральной базы нечего отправлять (нет замеров).";
            return;
        }

        await using var enqueue = conn.CreateCommand();
        enqueue.Transaction = tx;
        enqueue.CommandText = """
                              insert into central_ft.measurement_batches(source_plant, asset_code, prev_thk, prev_date, last_thk, last_date, last_label, last_note)
                              values (@plant, @code, @prev_thk, @prev_date, @last_thk, @last_date, @last_label, @last_note)
                              """;
        enqueue.Parameters.AddWithValue("plant", PlantCode);
        enqueue.Parameters.AddWithValue("code", assetCode);
        enqueue.Parameters.AddWithValue("prev_thk", (object?)prevThk ?? DBNull.Value);
        enqueue.Parameters.AddWithValue("prev_date", (object?)prevDate ?? DBNull.Value);
        enqueue.Parameters.AddWithValue("last_thk", lastThk.Value);
        enqueue.Parameters.AddWithValue("last_date", lastDate.Value);
        enqueue.Parameters.AddWithValue("last_label", (object?)lastLabel ?? DBNull.Value);
        enqueue.Parameters.AddWithValue("last_note", (object?)lastNote ?? DBNull.Value);
        await enqueue.ExecuteNonQueryAsync();
    }
}

public sealed record PlantMeasurementColumnViewModel(DateTimeOffset Date, string Label, string Header);

public sealed record EquipmentSortOption(string Code, string Title, string OrderBySql)
{
    public override string ToString() => Title;
}

public sealed record EquipmentGroupOption(string Code, string Title)
{
    public override string ToString() => Title;
}

public sealed record PlantMeasurementsGroupHeaderViewModel(string Title, int Count)
{
    public string DisplayTitle => $"{Title} ({Count})";
}

public sealed partial class PlantMeasurementEquipmentRowViewModel : ObservableObject
{
    private readonly Dictionary<(DateTimeOffset Date, string Label), decimal> valuesByKey = new();

    public PlantMeasurementEquipmentRowViewModel(string code, string? location, string? status, DateTimeOffset? createdAt, DateTimeOffset? lastMeasurementUtc)
    {
        Code = code;
        Location = location;
        Status = status;
        CreatedAt = createdAt;
        LastMeasurementUtc = lastMeasurementUtc;
        Cells = new ObservableCollection<string>();
    }

    public string Code { get; }

    public string? Location { get; }

    public string? Status { get; }

    public DateTimeOffset? CreatedAt { get; }

    public DateTimeOffset? LastMeasurementUtc { get; private set; }

    public ObservableCollection<string> Cells { get; }

    public void TryAddValue(DateTimeOffset ts, string label, decimal thickness)
    {
        var key = (ts, label);
        if (!valuesByKey.ContainsKey(key))
        {
            valuesByKey[key] = thickness;
        }

        if (LastMeasurementUtc is null || ts > LastMeasurementUtc.Value)
        {
            LastMeasurementUtc = ts;
        }
    }

    public void RebuildCells(IReadOnlyList<PlantMeasurementColumnViewModel> columns)
    {
        Cells.Clear();
        foreach (var col in columns)
        {
            var key = (col.Date, col.Label);
            if (valuesByKey.TryGetValue(key, out var thk))
            {
                Cells.Add(thk.ToString("0.###", CultureInfo.InvariantCulture));
            }
            else
            {
                Cells.Add("—");
            }
        }
    }
}
