using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OilErp.Core.Dto;
using OilErp.Ui.Services;
using Avalonia.Styling;
using OilErp.Ui.Views;

namespace OilErp.Ui.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly KernelGateway kernelGateway;
    private readonly string connectionString;

    public MainWindowViewModel()
    {
        kernelGateway = null!;
        connectionString = string.Empty;
        Profile = DatabaseProfile.Central;
        Title = "OilErp";
        Status = "Дизайн-режим";
        ConnectionDisplay = "Host=localhost;Database=central";
        ThemeOptions = BuildThemeOptions();
        SelectedTheme = ThemeOptions[0];
    }

    public MainWindowViewModel(KernelGateway kernelGateway, DatabaseProfile profile, string connectionString)
    {
        this.kernelGateway = kernelGateway ?? throw new ArgumentNullException(nameof(kernelGateway));
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        Profile = profile;
        var dbDisplay = ExtractDatabaseDisplayName(kernelGateway.ActualDatabase);
        Title = dbDisplay == "?" ? "OilErp" : $"OilErp — {dbDisplay}";

        ConnectionDisplay = SimplifyConnectionString(connectionString, kernelGateway.ActualDatabase);
        Status = kernelGateway.StatusMessage;
        ThemeOptions = BuildThemeOptions();
        SelectedTheme = ThemeOptions[0];

        var storage = kernelGateway.Storage;

        if (IsCentralProfile)
        {
            EquipmentCentral = new CentralEquipmentTabViewModel(storage, connectionString);
            PoliciesCentral = new CentralPoliciesTabViewModel(storage, connectionString);

            Analytics = new AnalyticsPanelViewModel(connectionString);
            MeasurementsCentral = new CentralMeasurementsTabViewModel(connectionString);

            _ = EquipmentCentral.RefreshAsync();
            _ = PoliciesCentral.RefreshAsync();
            _ = Analytics.LoadAsync();
            _ = MeasurementsCentral.RefreshAsync();
        }
        else if (IsPlantProfile)
        {
            EquipmentPlant = new PlantEquipmentTabViewModel(connectionString);
            MeasurementsPlant = new PlantMeasurementsTabViewModel(profile, storage, connectionString);
            _ = EquipmentPlant.RefreshAsync();
            _ = MeasurementsPlant.RefreshAsync();
        }
    }

    public DatabaseProfile Profile { get; }

    public bool IsCentralProfile => Profile == DatabaseProfile.Central;

    public bool IsPlantProfile => Profile is DatabaseProfile.PlantAnpz or DatabaseProfile.PlantKrnpz;

    [ObservableProperty] private string status;

    [ObservableProperty] private string title;

    [ObservableProperty] private string connectionDisplay;

    [ObservableProperty] private CentralEquipmentTabViewModel? equipmentCentral;

    [ObservableProperty] private PlantEquipmentTabViewModel? equipmentPlant;

    [ObservableProperty] private CentralPoliciesTabViewModel? policiesCentral;

    [ObservableProperty] private CentralMeasurementsTabViewModel? measurementsCentral;

    [ObservableProperty] private PlantMeasurementsTabViewModel? measurementsPlant;

    [ObservableProperty] private AnalyticsPanelViewModel? analytics;

    public IReadOnlyList<ThemeOption> ThemeOptions { get; }

    [ObservableProperty] private ThemeOption selectedTheme;

    [ObservableProperty] private bool isDataOperationInProgress;

    public event Action? RequestChangeConnection;

    [RelayCommand]
    private void ChangeConnection()
    {
        RequestChangeConnection?.Invoke();
    }

    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        if (value is null)
        {
            return;
        }

        ThemeManager.Apply(value.Palette, value.Variant);
    }

    partial void OnIsDataOperationInProgressChanged(bool value)
    {
        GenerateRandomDataCommand.NotifyCanExecuteChanged();
        ClearDatabaseCommand.NotifyCanExecuteChanged();
    }

    private bool CanRunDataOperations()
    {
        if (IsDataOperationInProgress) return false;
        return kernelGateway is not null && kernelGateway.IsLive;
    }

    [RelayCommand(CanExecute = nameof(CanRunDataOperations))]
    private async Task GenerateRandomDataAsync()
    {
        if (kernelGateway is null || string.IsNullOrWhiteSpace(connectionString))
        {
            Status = "Нет подключения к базе.";
            return;
        }

        try
        {
            IsDataOperationInProgress = true;
            Status = "Генерируем случайные данные...";

            var service = new RandomDataService(kernelGateway.Storage, connectionString, Profile);
            var result = await service.GenerateAsync(CancellationToken.None);
            await RefreshCurrentProfileAsync();

            var policyPart = result.Policies > 0 ? $", политики={result.Policies}" : string.Empty;
            Status = $"Готово: трубы={result.Assets}, замеры={result.Measurements}{policyPart}.";
        }
        catch (Exception ex)
        {
            Status = $"Ошибка генерации: {ex.Message}";
            AppLogger.Error($"[ui] random data generation error: {ex.Message}");
        }
        finally
        {
            IsDataOperationInProgress = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunDataOperations))]
    private async Task ClearDatabaseAsync()
    {
        if (kernelGateway is null || string.IsNullOrWhiteSpace(connectionString))
        {
            Status = "Нет подключения к базе.";
            return;
        }

        var confirmVm = new ConfirmDialogViewModel(
            "Очистить базу",
            "Вы уверены, что хотите полностью очистить базу? Данные будут удалены безвозвратно.",
            confirmText: "Да",
            cancelText: "Нет");

        var confirmDialog = new ConfirmDialogWindow { DataContext = confirmVm };
        var confirm = await UiDialogHost.ShowDialogAsync<bool?>(confirmDialog);
        if (confirm != true)
        {
            Status = "Отменено.";
            return;
        }

        try
        {
            IsDataOperationInProgress = true;
            Status = "Очищаем базу...";

            var service = new RandomDataService(kernelGateway.Storage, connectionString, Profile);
            await service.ClearAsync(CancellationToken.None);
            await RefreshCurrentProfileAsync();

            Status = "База очищена.";
        }
        catch (Exception ex)
        {
            Status = $"Ошибка очистки: {ex.Message}";
            AppLogger.Error($"[ui] clear database error: {ex.Message}");
        }
        finally
        {
            IsDataOperationInProgress = false;
        }
    }

    private async Task RefreshCurrentProfileAsync()
    {
        var tasks = new List<Task>();

        if (IsCentralProfile)
        {
            if (EquipmentCentral is not null) tasks.Add(EquipmentCentral.RefreshAsync());
            if (PoliciesCentral is not null) tasks.Add(PoliciesCentral.RefreshAsync());
            if (MeasurementsCentral is not null) tasks.Add(MeasurementsCentral.RefreshAsync());
            if (Analytics is not null) tasks.Add(Analytics.LoadAsync());
        }
        else if (IsPlantProfile)
        {
            if (EquipmentPlant is not null) tasks.Add(EquipmentPlant.RefreshAsync());
            if (MeasurementsPlant is not null) tasks.Add(MeasurementsPlant.RefreshAsync());
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    private static string SimplifyConnectionString(string connectionString)
        => SimplifyConnectionString(connectionString, actualDatabase: null);

    private static string SimplifyConnectionString(string connectionString, string? actualDatabase)
    {
        try
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
            var host = string.IsNullOrWhiteSpace(builder.Host) ? "?" : builder.Host;
            var db = ExtractDatabaseDisplayName(actualDatabase ?? builder.Database);
            var user = string.IsNullOrWhiteSpace(builder.Username) ? "?" : builder.Username;
            var port = builder.Port > 0 ? builder.Port.ToString() : "?";
            return $"{host}:{port} · {db} · {user}";
        }
        catch
        {
            return connectionString;
        }
    }

    private static string ExtractDatabaseDisplayName(string? databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName)) return "?";

        var raw = databaseName.Trim();
        var upper = raw.ToUpperInvariant();
        if (upper.Contains("ANPZ")) return "АНПЗ";
        if (upper.Contains("KRNPZ") || upper.Contains("KNPZ")) return "КНПЗ";
        if (upper.Contains("CENTRAL")) return "Центральная";
        return raw;
    }

    private static ThemeOption[] BuildThemeOptions() =>
        new[]
        {
            new ThemeOption("dark", "Тёмная", ThemePalette.UltraBlack, ThemeVariant.Dark),
            new ThemeOption("light", "Светлая", ThemePalette.JetBrainsLight, ThemeVariant.Light)
        };
}
