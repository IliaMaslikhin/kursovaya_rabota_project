using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using OilErp.Ui.ViewModels;

namespace OilErp.Ui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => WireVm();
        Opened += (_, _) => WireVm();
    }

    private void WireVm()
    {
        if (DataContext is not MainWindowViewModel vm) return;
        vm.RequestChangeConnection -= OnRequestChangeConnection;
        vm.RequestChangeConnection += OnRequestChangeConnection;
    }

    private void OnRequestChangeConnection()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var connectVm = new ConnectWindowViewModel(desktop);
        var connect = new ConnectWindow { DataContext = connectVm };
        connect.Show();
        desktop.MainWindow = connect;
        Close();
    }
}
