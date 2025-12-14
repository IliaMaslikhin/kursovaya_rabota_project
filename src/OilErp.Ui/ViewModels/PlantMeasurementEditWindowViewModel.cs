using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OilErp.Ui.ViewModels;

public sealed partial class PlantMeasurementEditWindowViewModel : ObservableObject
{
    public PlantMeasurementEditWindowViewModel(string title, string plantCode, string equipmentCode)
    {
        Title = title;
        PlantCode = plantCode;
        EquipmentCode = equipmentCode;

        label = "T1";
        thickness = 12.0;
        statusMessage = string.Empty;
    }

    public string Title { get; }

    public string PlantCode { get; }

    public string EquipmentCode { get; }

    [ObservableProperty] private string label;

    [ObservableProperty] private double thickness;

    [ObservableProperty] private string? note;

    [ObservableProperty] private string statusMessage;

    public event Action<PlantMeasurementEditResult?>? RequestClose;

    private bool CanSave() => !string.IsNullOrWhiteSpace(Label) && Thickness > 0;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            StatusMessage = "Укажите метку точки (label).";
            SaveCommand.NotifyCanExecuteChanged();
            return;
        }

        if (Thickness <= 0)
        {
            StatusMessage = "Толщина должна быть > 0.";
            SaveCommand.NotifyCanExecuteChanged();
            return;
        }

        var result = new PlantMeasurementEditResult(
            Label.Trim(),
            Thickness,
            string.IsNullOrWhiteSpace(Note) ? null : Note.Trim());

        RequestClose?.Invoke(result);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(null);
    }

    partial void OnLabelChanged(string value)
    {
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnThicknessChanged(double value)
    {
        SaveCommand.NotifyCanExecuteChanged();
    }
}

public sealed record PlantMeasurementEditResult(string Label, double Thickness, string? Note);
