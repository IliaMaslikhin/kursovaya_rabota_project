using System;
using Avalonia.Controls;
using OilErp.Ui.ViewModels;

namespace OilErp.Ui.Views;

public partial class PlantMeasurementHistoryWindow : Window
{
    private PlantMeasurementHistoryWindowViewModel? currentVm;

    public PlantMeasurementHistoryWindow()
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

        currentVm = DataContext as PlantMeasurementHistoryWindowViewModel;
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

