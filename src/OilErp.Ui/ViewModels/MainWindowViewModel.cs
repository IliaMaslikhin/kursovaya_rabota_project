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
        ConnectionForm = new ConnectionFormViewModel(OnConnectAsync);
    }

    public ConnectionFormViewModel ConnectionForm { get; }

    [ObservableProperty] private bool isConnected;

    [ObservableProperty] private string status;

    [ObservableProperty] private CentralDataEntryViewModel? dataEntry;

    [ObservableProperty] private AnalyticsPanelViewModel? analytics;

    private async Task OnConnectAsync(string connectionString)
    {
        try
        {
            Status = "Подключение...";
            kernelGateway = await Task.Run(() => KernelGateway.Create(connectionString));
            IsConnected = true;
            Status = $"Подключено к {connectionString}";
            AppLogger.Info($"[ui] подключение установлено: {connectionString}");
            DataEntry = new CentralDataEntryViewModel(kernelGateway.Storage);
            Analytics = new AnalyticsPanelViewModel(kernelGateway.Storage);
            await Analytics.LoadAsync();
        }
        catch (Exception ex)
        {
            IsConnected = false;
            Status = $"Ошибка подключения: {ex.Message}";
            AppLogger.Error($"[ui] connect failed: {ex.Message}");
        }
    }
}
