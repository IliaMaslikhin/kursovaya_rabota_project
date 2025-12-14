using Avalonia.Controls;
using OilErp.Ui.ViewModels;

namespace OilErp.Ui.Views;

public partial class ConnectWindow : Window
{
    public ConnectWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => WireVm();
        Opened += (_, _) => WireVm();
    }

    private void WireVm()
    {
        if (DataContext is not ConnectWindowViewModel vm) return;
        vm.RequestClose -= OnRequestClose;
        vm.RequestClose += OnRequestClose;
    }

    private void OnRequestClose()
    {
        Close();
    }
}

