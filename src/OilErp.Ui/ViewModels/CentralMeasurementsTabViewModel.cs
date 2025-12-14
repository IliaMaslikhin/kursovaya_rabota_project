using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npgsql;
using OilErp.Bootstrap;
using OilErp.Core.Contracts;
using OilErp.Core.Services.Central;
using OilErp.Ui.Services;
using OilErp.Ui.Views;

namespace OilErp.Ui.ViewModels;

public sealed partial class CentralMeasurementsTabViewModel : ObservableObject
{
    private const int MaxColumns = 12;
    private const string CentralPlantCode = "CENTRAL";

    private readonly IStoragePort storage;
    private readonly string connectionString;

    public CentralMeasurementsTabViewModel(IStoragePort storage, string connectionString)
    {
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        Columns = new ObservableCollection<CentralMeasurementColumnViewModel>();
        Rows = new ObservableCollection<CentralMeasurementEquipmentRowViewModel>();
        statusMessage = "Нажмите «Обновить» для загрузки таблицы.";
    }

    public ObservableCollection<CentralMeasurementColumnViewModel> Columns { get; }

    public ObservableCollection<CentralMeasurementEquipmentRowViewModel> Rows { get; }

    [ObservableProperty] private CentralMeasurementEquipmentRowViewModel? selectedRow;

    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private string statusMessage;

    partial void OnIsBusyChanged(bool value)
    {
        AddMeasurementCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedRowChanged(CentralMeasurementEquipmentRowViewModel? value)
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
            StatusMessage = "Загрузка оборудования и замеров (central)...";

            Rows.Clear();
            Columns.Clear();

            var rowsByCode = await LoadEquipmentAsync();
            await LoadCentralEventsAsync(rowsByCode);

            foreach (var row in Rows)
            {
                row.RebuildCells(Columns);
            }

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

        var assetCode = selected.Code.Trim();
        var baseTimestampUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

        var vm = new CentralMeasurementEditWindowViewModel($"Добавить замер (central)", assetCode);
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

            var timestampUtc = baseTimestampUtc;
            if (prevDate is not null && timestampUtc <= prevDate.Value)
            {
                timestampUtc = prevDate.Value.AddSeconds(1);
            }

            var payload = BuildPayload(assetCode, prevThk, prevDate, (decimal)Math.Round(result.Thickness, 3), timestampUtc);

            var enqueue = new FnEventsEnqueueService(storage);
            var ingest = new FnIngestEventsService(storage);

            StatusMessage = "Добавляем в очередь событий...";
            await enqueue.fn_events_enqueueAsync("HC_MEASUREMENT_BATCH", CentralPlantCode, payload, CancellationToken.None);

            StatusMessage = "Запускаем ingest...";
            await ingest.fn_ingest_eventsAsync(5000, CancellationToken.None);

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

    private async Task<Dictionary<string, CentralMeasurementEquipmentRowViewModel>> LoadEquipmentAsync()
    {
        var map = new Dictionary<string, CentralMeasurementEquipmentRowViewModel>(StringComparer.OrdinalIgnoreCase);

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            select asset_code, name, type
            from public.assets_global
            where plant_code is null or upper(plant_code) = 'CENTRAL'
            order by created_at desc
            limit 300
            """;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var code = reader.GetString(0);
            var name = reader.IsDBNull(1) ? null : reader.GetString(1);
            var type = reader.IsDBNull(2) ? null : reader.GetString(2);

            var row = new CentralMeasurementEquipmentRowViewModel(code, name, type);
            Rows.Add(row);
            map[code] = row;
        }

        return map;
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
              payload_json->>'asset_code' as asset_code,
              (payload_json->>'last_date')::timestamptz as ts,
              (payload_json->>'last_thk')::numeric as thickness
            from public.events_inbox
            where event_type = 'HC_MEASUREMENT_BATCH'
              and upper(coalesce(source_plant,'')) = 'CENTRAL'
              and payload_json ? 'asset_code'
              and payload_json ? 'last_date'
              and payload_json ? 'last_thk'
            order by (payload_json->>'last_date')::timestamptz desc, id desc
            limit 1500
            """;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var code = reader.GetString(0);
            if (!rowsByCode.TryGetValue(code, out var row)) continue;

            var dt = reader.GetFieldValue<DateTime>(1);
            if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            var ts = new DateTimeOffset(dt);
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

    private static string FormatColumn(DateTimeOffset ts)
    {
        var local = ts.ToLocalTime();
        return local.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);
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

    private static string BuildPayload(string assetCode, decimal? prevThk, DateTime? prevDateUtc, decimal lastThk, DateTime lastDateUtc)
    {
        var obj = new Dictionary<string, object?>
        {
            ["asset_code"] = assetCode,
            ["prev_thk"] = prevThk,
            ["prev_date"] = prevDateUtc,
            ["last_thk"] = lastThk,
            ["last_date"] = lastDateUtc
        };
        return JsonSerializer.Serialize(obj);
    }

    private sealed record AnalyticsLastState(DateTime? LastDateUtc, decimal? LastThickness);
}

public sealed record CentralMeasurementColumnViewModel(DateTimeOffset Timestamp, string Header);

public sealed partial class CentralMeasurementEquipmentRowViewModel : ObservableObject
{
    private readonly Dictionary<DateTimeOffset, decimal> valuesByTimestamp = new();

    public CentralMeasurementEquipmentRowViewModel(string code, string? name, string? type)
    {
        Code = code;
        Name = name;
        Type = type;
        Cells = new ObservableCollection<string>();
    }

    public string Code { get; }

    public string? Name { get; }

    public string? Type { get; }

    public ObservableCollection<string> Cells { get; }

    public void TryAddValue(DateTimeOffset ts, decimal thickness)
    {
        if (!valuesByTimestamp.ContainsKey(ts))
        {
            valuesByTimestamp[ts] = thickness;
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
}
