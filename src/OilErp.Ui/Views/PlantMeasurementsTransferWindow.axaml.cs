using System;
using Avalonia.Controls;
using OilErp.Ui.ViewModels;

namespace OilErp.Ui.Views;

public partial class PlantMeasurementsTransferWindow : Window
{
    private PlantMeasurementsTransferWindowViewModel? currentVm;

    public PlantMeasurementsTransferWindow()
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

        currentVm = DataContext as PlantMeasurementsTransferWindowViewModel;
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

