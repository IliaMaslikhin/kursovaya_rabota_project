using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        if (upper.Contains("ANPZ")) return "ANPZ";
        if (upper.Contains("KRNPZ") || upper.Contains("KNPZ")) return "KNPZ";
        if (upper.Contains("CENTRAL")) return "Central";
        return raw;
    }

    private static ThemeOption[] BuildThemeOptions() =>
        new[]
        {
            new ThemeOption("dark", "Тёмная", ThemePalette.UltraBlack, ThemeVariant.Dark),
            new ThemeOption("light", "Светлая", ThemePalette.JetBrainsLight, ThemeVariant.Light)
        };
}
