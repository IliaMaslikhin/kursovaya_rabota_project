using System;
using System.Collections.ObjectModel;
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

public sealed partial class CentralPoliciesTabViewModel : ObservableObject
{
    private readonly IStoragePort storage;
    private readonly string connectionString;
    private readonly DispatcherTimer filterDebounceTimer;
    private bool filterRefreshPending;

    public CentralPoliciesTabViewModel(IStoragePort storage, string connectionString)
    {
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        Items = new ObservableCollection<PolicyItemViewModel>();
        statusMessage = "Загрузите список политик.";

        filterDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        filterDebounceTimer.Tick += async (_, _) =>
        {
            filterDebounceTimer.Stop();
            if (IsBusy || !filterRefreshPending) return;
            filterRefreshPending = false;
            await RefreshAsync();
        };
    }

    public ObservableCollection<PolicyItemViewModel> Items { get; }

    [ObservableProperty] private PolicyItemViewModel? selectedItem;

    [ObservableProperty] private string filterText = string.Empty;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage;

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

    partial void OnSelectedItemChanged(PolicyItemViewModel? value)
    {
        if (value is null)
        {
            StatusMessage = "Выберите запись или нажмите «+» для добавления.";
        }
        else
        {
            StatusMessage = $"Выбрано: {value.Name}";
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
            StatusMessage = "Загрузка политик...";
            Items.Clear();

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                select name, threshold_low, threshold_med, threshold_high
                from public.risk_policies
                where @q is null
                   or name ilike @q
                order by name
                limit 200
                """;
            cmd.Parameters.Add("q", NpgsqlDbType.Text).Value = string.IsNullOrWhiteSpace(FilterText) ? DBNull.Value : $"%{FilterText.Trim()}%";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                var low = reader.IsDBNull(1) ? 0m : reader.GetFieldValue<decimal>(1);
                var med = reader.IsDBNull(2) ? 0m : reader.GetFieldValue<decimal>(2);
                var high = reader.IsDBNull(3) ? 0m : reader.GetFieldValue<decimal>(3);
                Items.Add(new PolicyItemViewModel(name, low, med, high));
            }

            StatusMessage = $"Загружено: {Items.Count}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
            AppLogger.Error($"[ui] policies refresh error: {ex.Message}");
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
            var vm = new RiskPolicyEditWindowViewModel(
                "Добавить политику риска",
                "default",
                isNameReadOnly: false,
                low: 0.001m,
                med: 0.005m,
                high: 0.010m);

            var dialog = new RiskPolicyEditWindow { DataContext = vm };
            var result = await UiDialogHost.ShowDialogAsync<RiskPolicyEditResult?>(dialog);
            if (result is null) return;

            await UpsertAsync(result.Name, result.Low, result.Med, result.High);
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
            var vm = new RiskPolicyEditWindowViewModel(
                $"Политика: {selected.Name}",
                selected.Name,
                isNameReadOnly: true,
                low: selected.Low,
                med: selected.Med,
                high: selected.High);

            var dialog = new RiskPolicyEditWindow { DataContext = vm };
            var result = await UiDialogHost.ShowDialogAsync<RiskPolicyEditResult?>(dialog);
            if (result is null) return;

            await UpsertAsync(selected.Name, result.Low, result.Med, result.High);
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
            StatusMessage = $"Удаляем {selected.Name}...";

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                delete from public.risk_policies
                where lower(name) = lower(@name)
                """;
            cmd.Parameters.AddWithValue("@name", selected.Name.Trim());
            var deleted = await cmd.ExecuteNonQueryAsync();

            StatusMessage = deleted > 0 ? "Удалено." : "Не найдено.";
            SelectedItem = null;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка удаления: {ex.Message}";
            AppLogger.Error($"[ui] policy delete error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            AddCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task UpsertAsync(string name, decimal low, decimal med, decimal high)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "Укажите имя политики.";
            return;
        }

        try
        {
            IsBusy = true;
            AddCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();

            var service = new FnPolicyUpsertService(storage);
            var affected = await service.fn_policy_upsertAsync(
                name.Trim(),
                low,
                med,
                high,
                CancellationToken.None);

            StatusMessage = $"Сохранено (строк={affected}). Обновляем...";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сохранения: {ex.Message}";
            AppLogger.Error($"[ui] policy save error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            AddCommand.NotifyCanExecuteChanged();
            EditCommand.NotifyCanExecuteChanged();
            DeleteCommand.NotifyCanExecuteChanged();
        }
    }
}

public sealed record PolicyItemViewModel(string Name, decimal Low, decimal Med, decimal High);
