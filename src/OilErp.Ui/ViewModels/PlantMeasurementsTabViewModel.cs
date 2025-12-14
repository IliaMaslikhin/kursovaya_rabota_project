using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npgsql;
using OilErp.Bootstrap;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Util;
using OilErp.Ui.Services;
using OilErp.Ui.Views;
using AnpzInsertService = OilErp.Core.Services.Plants.ANPZ.SpInsertMeasurementBatchService;
using KrnpzInsertService = OilErp.Core.Services.Plants.KRNPZ.SpInsertMeasurementBatchService;

namespace OilErp.Ui.ViewModels;

public sealed partial class PlantMeasurementsTabViewModel : ObservableObject
{
    private const int MaxColumns = 12;

    private readonly DatabaseProfile profile;
    private readonly IStoragePort storage;
    private readonly string connectionString;

    public PlantMeasurementsTabViewModel(DatabaseProfile profile, IStoragePort storage, string connectionString)
    {
        if (profile is not (DatabaseProfile.PlantAnpz or DatabaseProfile.PlantKrnpz))
            throw new ArgumentOutOfRangeException(nameof(profile), profile, "Plant profile expected");

        this.profile = profile;
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

        PlantCode = profile == DatabaseProfile.PlantKrnpz ? "KRNPZ" : "ANPZ";
        Columns = new ObservableCollection<PlantMeasurementColumnViewModel>();
        Rows = new ObservableCollection<PlantMeasurementEquipmentRowViewModel>();
        statusMessage = "Нажмите «Обновить» для загрузки таблицы.";
    }

    public string PlantCode { get; }

    public ObservableCollection<PlantMeasurementColumnViewModel> Columns { get; }

    public ObservableCollection<PlantMeasurementEquipmentRowViewModel> Rows { get; }

    [ObservableProperty] private PlantMeasurementEquipmentRowViewModel? selectedRow;

    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private string statusMessage;

    partial void OnIsBusyChanged(bool value)
    {
        AddMeasurementCommand.NotifyCanExecuteChanged();
    }

    private bool CanAddMeasurement() => !IsBusy && SelectedRow is not null;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            AddMeasurementCommand.NotifyCanExecuteChanged();
            StatusMessage = "Загрузка оборудования и замеров...";

            Rows.Clear();
            Columns.Clear();

            var rowsByCode = await LoadEquipmentAsync();
            await LoadMeasurementsAsync(rowsByCode);

            foreach (var row in Rows)
            {
                row.RebuildCells(Columns);
            }

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

        var vm = new PlantMeasurementEditWindowViewModel(
            "Добавить замер",
            PlantCode,
            assetCode);

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

            ApplyMeasurementToMatrix(selected, new DateTimeOffset(timestampUtc), (decimal)Math.Round(result.Thickness, 3));
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

    partial void OnSelectedRowChanged(PlantMeasurementEquipmentRowViewModel? value)
    {
        AddMeasurementCommand.NotifyCanExecuteChanged();
    }

    private async Task<Dictionary<string, PlantMeasurementEquipmentRowViewModel>> LoadEquipmentAsync()
    {
        var map = new Dictionary<string, PlantMeasurementEquipmentRowViewModel>(StringComparer.OrdinalIgnoreCase);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            select asset_code, location, status
            from public.assets_local
            order by created_at desc
            limit 300
            """;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var code = reader.GetString(0);
            var location = reader.IsDBNull(1) ? null : reader.GetString(1);
            var status = reader.IsDBNull(2) ? null : reader.GetString(2);

            var row = new PlantMeasurementEquipmentRowViewModel(code, location, status);
            Rows.Add(row);
            map[code] = row;
        }

        return map;
    }

    private async Task LoadMeasurementsAsync(Dictionary<string, PlantMeasurementEquipmentRowViewModel> rowsByCode)
    {
        if (rowsByCode.Count == 0) return;

        var columns = new List<DateTimeOffset>(MaxColumns);
        var columnsSet = new HashSet<DateTimeOffset>();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            select a.asset_code, m.ts, m.thickness
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
            var ts = new DateTimeOffset(tsValue);
            var thk = reader.GetFieldValue<decimal>(2);

            if (!columnsSet.Contains(ts))
            {
                if (columns.Count >= MaxColumns)
                {
                    continue;
                }

                columns.Add(ts);
                columnsSet.Add(ts);
            }

            row.TryAddValue(ts, thk);
        }

        Columns.Clear();
        foreach (var ts in columns)
        {
            Columns.Add(new PlantMeasurementColumnViewModel(ts, FormatColumn(ts)));
        }
    }

    private void ApplyMeasurementToMatrix(PlantMeasurementEquipmentRowViewModel row, DateTimeOffset ts, decimal thickness)
    {
        EnsureColumn(ts);
        row.TryAddValue(ts, thickness);

        foreach (var r in Rows)
        {
            r.RebuildCells(Columns);
        }
    }

    private void EnsureColumn(DateTimeOffset ts)
    {
        if (Columns.Any(c => c.Timestamp == ts)) return;

        var insertIndex = 0;
        while (insertIndex < Columns.Count && Columns[insertIndex].Timestamp > ts)
        {
            insertIndex++;
        }

        Columns.Insert(insertIndex, new PlantMeasurementColumnViewModel(ts, FormatColumn(ts)));

        while (Columns.Count > MaxColumns)
        {
            Columns.RemoveAt(Columns.Count - 1);
        }
    }

    private static string FormatColumn(DateTimeOffset ts)
    {
        var local = ts.ToLocalTime();
        return local.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);
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
}

public sealed record PlantMeasurementColumnViewModel(DateTimeOffset Timestamp, string Header);

public sealed partial class PlantMeasurementEquipmentRowViewModel : ObservableObject
{
    private readonly Dictionary<DateTimeOffset, decimal> valuesByTimestamp = new();

    public PlantMeasurementEquipmentRowViewModel(string code, string? location, string? status)
    {
        Code = code;
        Location = location;
        Status = status;
        Cells = new ObservableCollection<string>();
    }

    public string Code { get; }

    public string? Location { get; }

    public string? Status { get; }

    public ObservableCollection<string> Cells { get; }

    public void TryAddValue(DateTimeOffset ts, decimal thickness)
    {
        if (!valuesByTimestamp.ContainsKey(ts))
        {
            valuesByTimestamp[ts] = thickness;
        }
    }

    public void RebuildCells(IReadOnlyList<PlantMeasurementColumnViewModel> columns)
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
}
