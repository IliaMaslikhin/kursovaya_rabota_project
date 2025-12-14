using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OilErp.Ui.ViewModels;

public sealed partial class CentralMeasurementEditWindowViewModel : ObservableObject
{
    public CentralMeasurementEditWindowViewModel(string title, string equipmentCode)
    {
        Title = title;
        EquipmentCode = equipmentCode;
        thickness = 12.0;
        statusMessage = string.Empty;
    }

    public string Title { get; }

    public string EquipmentCode { get; }

    [ObservableProperty] private double thickness;

    [ObservableProperty] private string statusMessage;

    public event Action<CentralMeasurementEditResult?>? RequestClose;

    private bool CanSave() => Thickness > 0;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (Thickness <= 0)
        {
            StatusMessage = "Толщина должна быть > 0.";
            SaveCommand.NotifyCanExecuteChanged();
            return;
        }

        RequestClose?.Invoke(new CentralMeasurementEditResult(Thickness));
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(null);
    }

    partial void OnThicknessChanged(double value)
    {
        SaveCommand.NotifyCanExecuteChanged();
    }
}

public sealed record CentralMeasurementEditResult(double Thickness);

