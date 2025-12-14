using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using OilErp.Core.Dto;
using OilErp.Ui.Services;
using OilErp.Ui.Views;

namespace OilErp.Ui.ViewModels;

public sealed partial class ConnectWindowViewModel : ViewModelBase
{
    private readonly IClassicDesktopStyleApplicationLifetime? desktop;

    public ConnectWindowViewModel() : this(null) { }

    public ConnectWindowViewModel(IClassicDesktopStyleApplicationLifetime? desktop)
    {
        this.desktop = desktop;
        ConnectionForm = new ConnectionFormViewModel(ConnectAsync, ReadDefaultConnection());
    }

    public ConnectionFormViewModel ConnectionForm { get; }

    public event Action? RequestClose;

    private async Task ConnectAsync(DatabaseProfile profile, string connectionString)
    {
        var gateway = await Task.Run(() => KernelGateway.Create(connectionString, profile));
        if (desktop != null)
        {
            var effectiveConn = gateway.StorageConfig?.ConnectionString ?? connectionString;
            var mainVm = new MainWindowViewModel(gateway, profile, effectiveConn);
            var main = new MainWindow { DataContext = mainVm };
            main.Show();
            desktop.MainWindow = main;
            RequestClose?.Invoke();
        }
    }

    private static string? ReadDefaultConnection()
    {
        return Environment.GetEnvironmentVariable("OILERP__DB__CONN")
               ?? Environment.GetEnvironmentVariable("OIL_ERP_PG");
    }
}
