using System;
using Avalonia.Controls;
using OilErp.Ui.ViewModels;

namespace OilErp.Ui.Views;

public partial class ConfirmDialogWindow : Window
{
    private ConfirmDialogViewModel? currentVm;

    public ConfirmDialogWindow()
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

        currentVm = DataContext as ConfirmDialogViewModel;
        if (currentVm is not null)
        {
            currentVm.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose(bool? result)
    {
        Close(result);
    }
}

