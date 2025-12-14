using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OilErp.Bootstrap;
using OilErp.Core.Dto;
using OilErp.Ui.Services;
using Avalonia.Styling;

namespace OilErp.Ui.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly KernelGateway kernelGateway;

    public MainWindowViewModel()
    {
        kernelGateway = null!;
        Profile = DatabaseProfile.Central;
        Title = "OilErp";
        Status = "Design-time";
        ConnectionDisplay = "Host=localhost;Database=central";
        ThemeOptions = BuildThemeOptions();
        SelectedTheme = ThemeOptions[0];
    }

    public MainWindowViewModel(KernelGateway kernelGateway, DatabaseProfile profile, string connectionString)
    {
        this.kernelGateway = kernelGateway ?? throw new ArgumentNullException(nameof(kernelGateway));
        Profile = profile;
        Title = profile switch
        {
            DatabaseProfile.Central => "OilErp — Central",
            DatabaseProfile.PlantAnpz => "OilErp — ANPZ",
            DatabaseProfile.PlantKrnpz => "OilErp — KRNPZ",
            _ => "OilErp"
        };

        ConnectionDisplay = SimplifyConnectionString(connectionString);
        Status = kernelGateway.StatusMessage;
        ThemeOptions = BuildThemeOptions();
        SelectedTheme = ThemeOptions[0];

        var storage = kernelGateway.Storage;
        var factory = new StoragePortFactory(storage);
        Diagnostics = new DiagnosticsPanelViewModel(factory);

        if (IsCentralProfile)
        {
            EquipmentCentral = new CentralEquipmentTabViewModel(storage, connectionString);
            PoliciesCentral = new CentralPoliciesTabViewModel(storage, connectionString);

            Analytics = new AnalyticsPanelViewModel(storage);
            EventQueue = new EventQueueViewModel(storage);
            MeasurementsCentral = new CentralMeasurementsTabViewModel(storage, connectionString);

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

    [ObservableProperty] private EventQueueViewModel? eventQueue;

    [ObservableProperty] private DiagnosticsPanelViewModel? diagnostics;

    public IReadOnlyList<ThemeOption> ThemeOptions { get; }

    [ObservableProperty] private ThemeOption selectedTheme;

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

    private static string SimplifyConnectionString(string connectionString)
    {
        try
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
            var host = string.IsNullOrWhiteSpace(builder.Host) ? "?" : builder.Host;
            var db = string.IsNullOrWhiteSpace(builder.Database) ? "?" : builder.Database;
            var user = string.IsNullOrWhiteSpace(builder.Username) ? "?" : builder.Username;
            var port = builder.Port > 0 ? builder.Port.ToString() : "?";
            return $"{host}:{port} · {db} · {user}";
        }
        catch
        {
            return connectionString;
        }
    }

    private static ThemeOption[] BuildThemeOptions() =>
        new[]
        {
            new ThemeOption("ultra-black", "Ultra black", ThemePalette.UltraBlack, ThemeVariant.Dark),
            new ThemeOption("jetbrains-light", "JetBrains light", ThemePalette.JetBrainsLight, ThemeVariant.Light)
        };
}
