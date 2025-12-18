using System;
using Avalonia.Controls;
using OilErp.Ui.ViewModels;

namespace OilErp.Ui.Views;

public partial class CentralMeasurementHistoryWindow : Window
{
    private CentralMeasurementHistoryWindowViewModel? currentVm;

    public CentralMeasurementHistoryWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (currentVm is not null)
        {
            currentVm.RequestClose -= OnRequestClose;
        }

        currentVm = DataContext as CentralMeasurementHistoryWindowViewModel;
        if (currentVm is not null)
        {
            currentVm.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose(bool result)
    {
        Close(result);
    }
}

