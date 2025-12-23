using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

public sealed partial class PlantEquipmentTabViewModel : ObservableObject
{
    private readonly string connectionString;
    private static readonly EquipmentSortOption[] EquipmentSortOptionsSource =
    {
        new EquipmentSortOption("created_desc", "Создано (новые)", "created_at desc"),
        new EquipmentSortOption("created_asc", "Создано (старые)", "created_at asc"),
        new EquipmentSortOption("code", "Код (A→Z)", "asset_code"),
        new EquipmentSortOption("location", "Локация (A→Z)", "location"),
        new EquipmentSortOption("status", "Статус (A→Z)", "status")
    };
    private static readonly EquipmentGroupOption[] EquipmentGroupOptionsSource =
    {
        new EquipmentGroupOption("none", "Без группировки"),
        new EquipmentGroupOption("status", "Группировать: статус"),
        new EquipmentGroupOption("location", "Группировать: локация"),
        new EquipmentGroupOption("created_day", "Группировать: дата создания"),
        new EquipmentGroupOption("code_prefix", "Группировать: код (префикс)")
    };
    private static readonly string[] StatusOptions = { "Норма", "Предупреждение", "Критично", "Неизвестно" };
    private readonly DispatcherTimer filterDebounceTimer;
    private bool filterRefreshPending;

    public PlantEquipmentTabViewModel(string connectionString)
    {
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        Items = new ObservableCollection<PlantEquipmentItemViewModel>();
        DisplayItems = new ObservableCollection<object>();
        statusMessage = "Загрузите список оборудования.";
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

    public ObservableCollection<PlantEquipmentItemViewModel> Items { get; }

    public ObservableCollection<object> DisplayItems { get; }

    public IReadOnlyList<EquipmentSortOption> EquipmentSortOptions { get; }

    public IReadOnlyList<EquipmentGroupOption> EquipmentGroupOptions { get; }

    [ObservableProperty] private object? selectedDisplayItem;

    [ObservableProperty] private PlantEquipmentItemViewModel? selectedItem;

    [ObservableProperty] private string filterText = string.Empty;

    [ObservableProperty] private EquipmentSortOption selectedEquipmentSort;

    [ObservableProperty] private EquipmentGroupOption selectedEquipmentGroup;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage;

    partial void OnSelectedDisplayItemChanged(object? value)
    {
        SelectedItem = value as PlantEquipmentItemViewModel;
    }

    partial void OnIsBusyChanged(bool value)
    {
        AddCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();

        if (!value && filterRefreshPending)
        {
            filterDebounceTimer.Stop();
            filterDebounceTimer.Start();
        }
    }

    partial void OnSelectedItemChanged(PlantEquipmentItemViewModel? value)
    {
        if (value is null)
        {
            StatusMessage = "Выберите запись или нажмите «Добавить» для добавления.";
        }
        else
        {
            StatusMessage = $"Выбрано: {value.Code}";
        }

        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    partial void OnFilterTextChanged(string value)
    {
        filterRefreshPending = true;
        filterDebounceTimer.Stop();
        filterDebounceTimer.Start();
    }

    partial void OnSelectedEquipmentSortChanged(EquipmentSortOption value)
    {
        RebuildDisplayItems();
    }

    partial void OnSelectedEquipmentGroupChanged(EquipmentGroupOption value)
    {
        RebuildDisplayItems();
    }

    private bool CanOpenDialog() => !IsBusy;

    private bool CanEditSelected()
    {
        if (IsBusy) return false;
        return SelectedItem is not null;
    }

    private bool CanDeleteSelected()
    {
        if (IsBusy) return false;
        return SelectedItem is not null;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Загрузка оборудования...";
            SelectedDisplayItem = null;
            SelectedItem = null;
            Items.Clear();
            DisplayItems.Clear();

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                select asset_code, location, status, created_at
                from public.assets_local
                where @q is null
                   or asset_code ilike @q
                   or coalesce(location,'') ilike @q
                   or coalesce(status,'') ilike @q
                order by created_at desc
                limit 500
                """;
            cmd.Parameters.Add("q", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(FilterText) ? DBNull.Value : $"%{FilterText.Trim()}%";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var code = reader.GetString(0);
                var locationValue = reader.IsDBNull(1) ? null : reader.GetString(1);
                var statusValue = reader.IsDBNull(2) ? null : reader.GetString(2);
                DateTimeOffset? createdAt = null;
                if (!reader.IsDBNull(3))
                {
                    var dt = reader.GetFieldValue<DateTime>(3);
                    if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                    createdAt = new DateTimeOffset(dt);
                }
                Items.Add(new PlantEquipmentItemViewModel(code, locationValue, NormalizeStatus(statusValue), createdAt));
            }

            RebuildDisplayItems();
            StatusMessage = $"Загружено: {Items.Count}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
            AppLogger.Error($"[ui] plant equipment refresh error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        FilterText = string.Empty;
        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanOpenDialog))]
    public async Task AddAsync()
    {
        try
        {
            var vm = new EquipmentEditWindowViewModel(
                "Добавить оборудование (завод)",
                "Код оборудования",
                string.Empty,
                isCodeReadOnly: false,
                "Локация",
                null,
                "Статус",
                "Норма",
                field2Options: StatusOptions);

            var dialog = new EquipmentEditWindow { DataContext = vm };
            var result = await UiDialogHost.ShowDialogAsync<EquipmentEditResult?>(dialog);
            if (result is null) return;

            await UpsertAsync(result.Code, result.Field1, result.Field2);
        }
        finally
        {
            AddCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditSelected))]
    public async Task EditAsync()
    {
        var selected = SelectedItem;
        if (selected is null) return;

        try
        {
            var vm = new EquipmentEditWindowViewModel(
                $"Оборудование: {selected.Code}",
                "Код оборудования",
                selected.Code,
                isCodeReadOnly: true,
                "Локация",
                selected.Location,
                "Статус",
                selected.Status,
                field2Options: StatusOptions);

            var dialog = new EquipmentEditWindow { DataContext = vm };
            var result = await UiDialogHost.ShowDialogAsync<EquipmentEditResult?>(dialog);
            if (result is null) return;

            await UpsertAsync(selected.Code, result.Field1, result.Field2);
        }
        finally
        {
            EditCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    public async Task DeleteAsync()
    {
        var selected = SelectedItem;
        if (selected is null) return;

        try
        {
            IsBusy = true;
            AddCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            StatusMessage = $"Удаляем {selected.Code}...";

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                delete from public.assets_local
                where lower(asset_code) = lower(@code)
                """;
            cmd.Parameters.AddWithValue("@code", selected.Code.Trim());

            var deleted = await cmd.ExecuteNonQueryAsync();
            StatusMessage = deleted > 0 ? "Удалено." : "Не найдено.";
            SelectedItem = null;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка удаления: {ex.Message}";
            AppLogger.Error($"[ui] plant equipment delete error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            AddCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task UpsertAsync(string code, string? location, string? status)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            StatusMessage = "Укажите код оборудования.";
            return;
        }

        var normalizedStatus = NormalizeStatus(status);

        try
        {
            IsBusy = true;
            AddCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                with upd as (
                  update public.assets_local
                  set location = @location,
                      status   = @status
                  where lower(asset_code) = lower(@code)
                  returning 1
                )
                insert into public.assets_local(asset_code, location, status)
                select @code, @location, @status
                where not exists (select 1 from upd)
                """;
            cmd.Parameters.AddWithValue("@code", code.Trim());
            cmd.Parameters.AddWithValue("@location", (object?)location?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@status", normalizedStatus);

            await cmd.ExecuteNonQueryAsync();
            StatusMessage = "Сохранено. Обновляем список...";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сохранения: {ex.Message}";
            AppLogger.Error($"[ui] plant equipment save error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            AddCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }
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

    private void RebuildDisplayItems()
    {
        DisplayItems.Clear();
        if (Items.Count == 0) return;

        var ordered = GetSortedItems();
        if (string.Equals(SelectedEquipmentGroup.Code, "none", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var item in ordered)
            {
                DisplayItems.Add(item);
            }

            return;
        }

        if (string.Equals(SelectedEquipmentGroup.Code, "status", StringComparison.OrdinalIgnoreCase))
        {
            var groups = ordered
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Status) ? "—" : i.Status.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var g in groups)
            {
                DisplayItems.Add(new EquipmentGroupHeaderViewModel($"Статус: {g.Key}", g.Count()));
                foreach (var item in g)
                {
                    DisplayItems.Add(item);
                }
            }

            return;
        }

        if (string.Equals(SelectedEquipmentGroup.Code, "location", StringComparison.OrdinalIgnoreCase))
        {
            var groups = ordered
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Location) ? "—" : i.Location.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var g in groups)
            {
                DisplayItems.Add(new EquipmentGroupHeaderViewModel($"Локация: {g.Key}", g.Count()));
                foreach (var item in g)
                {
                    DisplayItems.Add(item);
                }
            }

            return;
        }

        if (string.Equals(SelectedEquipmentGroup.Code, "created_day", StringComparison.OrdinalIgnoreCase))
        {
            var groups = ordered
                .GroupBy(i => i.CreatedAt?.ToLocalTime().Date)
                .OrderByDescending(g => g.Key ?? DateTime.MinValue);

            foreach (var g in groups)
            {
                var title = g.Key.HasValue ? g.Key.Value.ToString("dd.MM.yyyy") : "—";
                DisplayItems.Add(new EquipmentGroupHeaderViewModel($"Дата: {title}", g.Count()));
                foreach (var item in g)
                {
                    DisplayItems.Add(item);
                }
            }

            return;
        }

        if (string.Equals(SelectedEquipmentGroup.Code, "code_prefix", StringComparison.OrdinalIgnoreCase))
        {
            var groups = ordered
                .GroupBy(i => GetCodePrefix(i.Code), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var g in groups)
            {
                DisplayItems.Add(new EquipmentGroupHeaderViewModel($"Префикс: {g.Key}", g.Count()));
                foreach (var item in g)
                {
                    DisplayItems.Add(item);
                }
            }
        }
    }

    private IReadOnlyList<PlantEquipmentItemViewModel> GetSortedItems()
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        return SelectedEquipmentSort.Code switch
        {
            "created_asc" => Items
                .OrderBy(i => i.CreatedAt ?? DateTimeOffset.MinValue)
                .ThenBy(i => i.Code, comparer)
                .ToList(),
            "code" => Items.OrderBy(i => i.Code, comparer).ToList(),
            "location" => Items
                .OrderBy(i => i.Location ?? string.Empty, comparer)
                .ThenBy(i => i.Code, comparer)
                .ToList(),
            "status" => Items
                .OrderBy(i => i.Status ?? string.Empty, comparer)
                .ThenBy(i => i.Code, comparer)
                .ToList(),
            _ => Items
                .OrderByDescending(i => i.CreatedAt ?? DateTimeOffset.MinValue)
                .ThenBy(i => i.Code, comparer)
                .ToList()
        };
    }

    private static string GetCodePrefix(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "—";
        var trimmed = code.Trim();
        var idx = trimmed.IndexOf('-');
        return idx > 0 ? trimmed[..idx] : trimmed;
    }
}

public sealed record PlantEquipmentItemViewModel(string Code, string? Location, string? Status, DateTimeOffset? CreatedAt);
