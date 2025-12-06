using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using OilErp.Bootstrap;
using OilErp.Ui.Services;

namespace OilErp.Ui.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private KernelGateway? kernelGateway;

    public MainWindowViewModel()
    {
        Status = "Подключение не установлено";
        ConnectionForm = new ConnectionFormViewModel(OnConnectAsync, ReadDefaultConnection());
    }

    public ConnectionFormViewModel ConnectionForm { get; }

    [ObservableProperty] private bool isConnected;

    [ObservableProperty] private string status;

    [ObservableProperty] private CentralDataEntryViewModel? dataEntry;

    [ObservableProperty] private AnalyticsPanelViewModel? analytics;

    [ObservableProperty] private MeasurementsPanelViewModel? measurements;

    [ObservableProperty] private DiagnosticsPanelViewModel? diagnostics;

    private async Task OnConnectAsync(string connectionString)
    {
        try
        {
            Status = "Подключение...";
            AppLogger.Info($"[ui] start connect flow");
            kernelGateway = await Task.Run(() => KernelGateway.Create(connectionString));
            IsConnected = true;
            var profileText = kernelGateway.BootstrapInfo?.Profile.ToString() ?? "unknown";
            Status = $"Подключено ({profileText}) {connectionString}";
            AppLogger.Info($"[ui] подключение установлено: {connectionString}");
            var storageFactory = kernelGateway.StorageFactory;
            var snapshotService = MeasurementSnapshotService.CreateDefault();
            var dataProvider = new MeasurementDataProvider(kernelGateway.Storage, snapshotService);
            var ingestionService = new MeasurementIngestionService(storageFactory);

            DataEntry = new CentralDataEntryViewModel(kernelGateway.Storage);
            Analytics = new AnalyticsPanelViewModel(kernelGateway.Storage);
            Measurements = new MeasurementsPanelViewModel(dataProvider, snapshotService, ingestionService);
            Diagnostics = new DiagnosticsPanelViewModel(storageFactory);
            AppLogger.Info("[ui] launching analytics load");
            await Analytics.LoadAsync();
            AppLogger.Info("[ui] launching measurements load");
            await Measurements.LoadAsync();
            AppLogger.Info("[ui] connect flow completed");
        }
        catch (Exception ex)
        {
            IsConnected = false;
            Status = $"Ошибка подключения: {ex.Message}";
            AppLogger.Error($"[ui] connect failed: {ex.Message}");
        }
    }

    private static string? ReadDefaultConnection()
    {
        return Environment.GetEnvironmentVariable("OILERP__DB__CONN")
               ?? Environment.GetEnvironmentVariable("OIL_ERP_PG");
    }
}
