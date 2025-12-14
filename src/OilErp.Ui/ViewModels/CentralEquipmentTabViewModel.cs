using System;
using System.Collections.ObjectModel;
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

public sealed partial class CentralEquipmentTabViewModel : ObservableObject
{
    private const string CentralPlantCode = "CENTRAL";

    private readonly IStoragePort storage;
    private readonly string connectionString;

    public CentralEquipmentTabViewModel(IStoragePort storage, string connectionString)
    {
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

        Items = new ObservableCollection<EquipmentItemViewModel>();
        statusMessage = "Загрузите список оборудования.";
    }

    public ObservableCollection<EquipmentItemViewModel> Items { get; }

    [ObservableProperty] private EquipmentItemViewModel? selectedItem;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage;

    partial void OnIsBusyChanged(bool value)
    {
        AddCommand.NotifyCanExecuteChanged();
        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedItemChanged(EquipmentItemViewModel? value)
    {
        if (value is null)
        {
            StatusMessage = "Выберите запись или нажмите «+» для добавления.";
        }
        else if (!IsEditableInCentral(value))
        {
            var plant = string.IsNullOrWhiteSpace(value.PlantCode) ? "—" : value.PlantCode;
            StatusMessage = $"Оборудование получено из {plant}. Редактирование доступно только на заводе.";
        }
        else
        {
            StatusMessage = $"Выбрано: {value.Code}";
        }

        EditCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
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
            Items.Clear();

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                select asset_code, name, type, plant_code
                from public.assets_global
                order by asset_code
                limit 500
                """;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var code = reader.GetString(0);
                var name = reader.IsDBNull(1) ? null : reader.GetString(1);
                var type = reader.IsDBNull(2) ? null : reader.GetString(2);
                var plant = reader.IsDBNull(3) ? null : reader.GetString(3);
                Items.Add(new EquipmentItemViewModel(code, name, type, plant));
            }

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

    [RelayCommand(CanExecute = nameof(CanOpenDialog))]
    public async Task AddAsync()
    {
        try
        {
            var vm = new EquipmentEditWindowViewModel(
                "Добавить оборудование (central)",
                "Код оборудования",
                string.Empty,
                isCodeReadOnly: false,
                "Название",
                null,
                "Тип",
                "PIPELINE");

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
            StatusMessage = "Эта запись пришла с завода. Редактирование недоступно в Central.";
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
            StatusMessage = "Эта запись пришла с завода. Удаление недоступно в Central.";
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
            StatusMessage = deleted > 0 ? "Удалено." : "Не найдено (возможно, не central).";
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
}

public sealed record EquipmentItemViewModel(string Code, string? Name, string? Type, string? PlantCode);
