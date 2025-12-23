using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npgsql;
using NpgsqlTypes;
using OilErp.Bootstrap;
using OilErp.Core.Contracts;
using OilErp.Core.Services.Central;
using OilErp.Ui.Services;
using OilErp.Ui.Views;

namespace OilErp.Ui.ViewModels;

public sealed partial class CentralEquipmentTabViewModel : ObservableObject
{
    private const string CentralPlantCode = "CENTRAL";
    private static readonly EquipmentSortOption[] EquipmentSortOptionsSource =
    {
        new EquipmentSortOption("code", "Код (A→Z)", "asset_code"),
        new EquipmentSortOption("name", "Название (A→Z)", "name"),
        new EquipmentSortOption("type", "Тип (A→Z)", "type"),
        new EquipmentSortOption("plant", "Завод (A→Z)", "plant_code")
    };
    private static readonly EquipmentGroupOption[] EquipmentGroupOptionsSource =
    {
        new EquipmentGroupOption("none", "Без группировки"),
        new EquipmentGroupOption("plant", "Группировать: завод"),
        new EquipmentGroupOption("type", "Группировать: тип"),
        new EquipmentGroupOption("code_prefix", "Группировать: код (префикс)")
    };

    private readonly IStoragePort storage;
    private readonly string connectionString;
    private readonly DispatcherTimer filterDebounceTimer;
    private bool filterRefreshPending;

    public CentralEquipmentTabViewModel(IStoragePort storage, string connectionString)
    {
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

        Items = new ObservableCollection<EquipmentItemViewModel>();
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

    public ObservableCollection<EquipmentItemViewModel> Items { get; }

    public ObservableCollection<object> DisplayItems { get; }

    public IReadOnlyList<EquipmentSortOption> EquipmentSortOptions { get; }

    public IReadOnlyList<EquipmentGroupOption> EquipmentGroupOptions { get; }

    [ObservableProperty] private object? selectedDisplayItem;

    [ObservableProperty] private EquipmentItemViewModel? selectedItem;

    [ObservableProperty] private string filterText = string.Empty;

    [ObservableProperty] private EquipmentSortOption selectedEquipmentSort;

    [ObservableProperty] private EquipmentGroupOption selectedEquipmentGroup;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage;

    partial void OnSelectedDisplayItemChanged(object? value)
    {
        SelectedItem = value as EquipmentItemViewModel;
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

    partial void OnSelectedItemChanged(EquipmentItemViewModel? value)
    {
        if (value is null)
        {
            StatusMessage = "Выберите запись или нажмите «+» для добавления.";
        }
        else if (!IsEditableInCentral(value))
        {
            var plant = FormatPlantDisplay(value.PlantCode);
            StatusMessage = $"Оборудование получено из {plant}. Редактирование доступно только на заводе.";
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
        return SelectedItem is not null && IsEditableInCentral(SelectedItem);
    }

    private bool CanDeleteSelected()
    {
        if (IsBusy) return false;
        return SelectedItem is not null && IsEditableInCentral(SelectedItem);
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
                select asset_code, name, type, plant_code
                from public.assets_global
                where @q is null
                   or asset_code ilike @q
                   or coalesce(name,'') ilike @q
                   or coalesce(type,'') ilike @q
                   or coalesce(plant_code,'') ilike @q
                order by asset_code
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
                Items.Add(new EquipmentItemViewModel(code, name, type, plant));
            }

            RebuildDisplayItems();
            StatusMessage = $"Загружено: {Items.Count}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
            AppLogger.Error($"[ui] equipment refresh error: {ex.Message}");
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
                "Добавить оборудование (центральная база)",
                "Код оборудования",
                string.Empty,
                isCodeReadOnly: false,
                "Название",
                null,
                "Тип",
                "ТРУБА");

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
        if (!IsEditableInCentral(selected))
        {
            StatusMessage = "Эта запись пришла с завода. Редактирование недоступно в центральной базе.";
            return;
        }

        try
        {
            var vm = new EquipmentEditWindowViewModel(
                $"Оборудование: {selected.Code}",
                "Код оборудования",
                selected.Code,
                isCodeReadOnly: true,
                "Название",
                selected.Name,
                "Тип",
                selected.Type);

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
        if (!IsEditableInCentral(selected))
        {
            StatusMessage = "Эта запись пришла с завода. Удаление недоступно в центральной базе.";
            return;
        }

        try
        {
            IsBusy = true;
            AddCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
            StatusMessage = $"Удаляем {selected.Code}...";

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "delete from public.analytics_cr where lower(asset_code) = lower(@code);";
                cmd.Parameters.AddWithValue("@code", selected.Code.Trim());
                await cmd.ExecuteNonQueryAsync();
            }

            int deleted;
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    delete from public.assets_global
                    where lower(asset_code) = lower(@code)
                      and (plant_code is null or upper(plant_code) = 'CENTRAL')
                    """;
                cmd.Parameters.AddWithValue("@code", selected.Code.Trim());
                deleted = await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            StatusMessage = deleted > 0 ? "Удалено." : "Не найдено (возможно, не центральная база).";
            SelectedItem = null;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка удаления: {ex.Message}";
            AppLogger.Error($"[ui] equipment delete error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            AddCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task UpsertAsync(string code, string? name, string? type)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            StatusMessage = "Укажите код оборудования.";
            return;
        }

        try
        {
            IsBusy = true;
            AddCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();

            var service = new FnAssetUpsertService(storage);
            var affected = await service.fn_asset_upsertAsync(
                code.Trim(),
                string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
                string.IsNullOrWhiteSpace(type) ? null : type.Trim(),
                CentralPlantCode,
                CancellationToken.None);

            StatusMessage = $"Сохранено (строк={affected}). Обновляем список...";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сохранения: {ex.Message}";
            AppLogger.Error($"[ui] equipment save error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            AddCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }
    }

    private static bool IsEditableInCentral(EquipmentItemViewModel item)
    {
        if (item is null) return false;
        return string.IsNullOrWhiteSpace(item.PlantCode)
               || string.Equals(item.PlantCode, CentralPlantCode, StringComparison.OrdinalIgnoreCase);
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

        if (string.Equals(SelectedEquipmentGroup.Code, "plant", StringComparison.OrdinalIgnoreCase))
        {
            var groups = ordered
                .GroupBy(i => string.IsNullOrWhiteSpace(i.PlantCode) ? "—" : i.PlantCode.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var g in groups)
            {
                var plantTitle = g.Key == "—" ? g.Key : FormatPlantDisplay(g.Key);
                DisplayItems.Add(new EquipmentGroupHeaderViewModel($"Завод: {plantTitle}", g.Count()));
                foreach (var item in g)
                {
                    DisplayItems.Add(item);
                }
            }

            return;
        }

        if (string.Equals(SelectedEquipmentGroup.Code, "type", StringComparison.OrdinalIgnoreCase))
        {
            var groups = ordered
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Type) ? "—" : i.Type.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var g in groups)
            {
                DisplayItems.Add(new EquipmentGroupHeaderViewModel($"Тип: {g.Key}", g.Count()));
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

    private IReadOnlyList<EquipmentItemViewModel> GetSortedItems()
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        return SelectedEquipmentSort.Code switch
        {
            "name" => Items.OrderBy(i => i.Name ?? string.Empty, comparer).ThenBy(i => i.Code, comparer).ToList(),
            "type" => Items.OrderBy(i => i.Type ?? string.Empty, comparer).ThenBy(i => i.Code, comparer).ToList(),
            "plant" => Items
                .OrderBy(i => string.IsNullOrWhiteSpace(i.PlantCode))
                .ThenBy(i => i.PlantCode ?? string.Empty, comparer)
                .ThenBy(i => i.Code, comparer)
                .ToList(),
            _ => Items.OrderBy(i => i.Code, comparer).ToList()
        };
    }

    private static string GetCodePrefix(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "—";
        var trimmed = code.Trim();
        var idx = trimmed.IndexOf('-');
        return idx > 0 ? trimmed[..idx] : trimmed;
    }

    internal static string FormatPlantDisplay(string? plantCode)
    {
        if (string.IsNullOrWhiteSpace(plantCode)) return "—";
        var upper = plantCode.Trim().ToUpperInvariant();
        return upper switch
        {
            "KRNPZ" or "KNPZ" => "КНПЗ",
            "ANPZ" => "АНПЗ",
            "CENTRAL" => "Центральная",
            _ => upper
        };
    }
}

public sealed record EquipmentItemViewModel(string Code, string? Name, string? Type, string? PlantCode)
{
    public string PlantCodeDisplay => CentralEquipmentTabViewModel.FormatPlantDisplay(PlantCode);
}
