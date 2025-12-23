using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OilErp.Ui.ViewModels;

public sealed partial class PlantMeasurementEditWindowViewModel : ObservableObject
{
    public PlantMeasurementEditWindowViewModel(
        string title,
        string plantCode,
        string equipmentCode,
        string label,
        double? initialThickness = null,
        string? initialNote = null,
        bool isLabelReadOnly = false)
    {
        Title = title;
        PlantCode = plantCode;
        EquipmentCode = equipmentCode;

        Label = string.IsNullOrWhiteSpace(label) ? "T1" : label.Trim();
        thicknessText = (initialThickness ?? 12.0).ToString("0.###", CultureInfo.InvariantCulture);
        note = string.IsNullOrWhiteSpace(initialNote) ? null : initialNote.Trim();
        IsLabelReadOnly = isLabelReadOnly;
        statusMessage = string.Empty;
    }

    public string Title { get; }

    public string PlantCode { get; }

    public string PlantCodeDisplay => PlantCode switch
    {
        "ANPZ" => "АНПЗ",
        "KNPZ" or "KRNPZ" => "КНПЗ",
        _ => PlantCode
    };

    public string EquipmentCode { get; }

    public string Label { get; }

    [ObservableProperty] private string thicknessText;

    [ObservableProperty] private string? note;

    [ObservableProperty] private string statusMessage;

    public bool IsLabelReadOnly { get; }

    public event Action<PlantMeasurementEditResult?>? RequestClose;

    private bool CanSave() =>
        TryParseThickness(ThicknessText, out var thk)
        && thk > 0;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            StatusMessage = "Не получилось сгенерировать метку точки.";
            SaveCommand.NotifyCanExecuteChanged();
            return;
        }

        if (!TryParseThickness(ThicknessText, out var thickness))
        {
            StatusMessage = "Толщина должна быть числом (например: 12.5).";
            SaveCommand.NotifyCanExecuteChanged();
            return;
        }

        if (thickness <= 0)
        {
            StatusMessage = "Толщина должна быть > 0.";
            SaveCommand.NotifyCanExecuteChanged();
            return;
        }

        var result = new PlantMeasurementEditResult(
            Label.Trim(),
            thickness,
            string.IsNullOrWhiteSpace(Note) ? null : Note.Trim());

        RequestClose?.Invoke(result);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(null);
    }

    partial void OnThicknessTextChanged(string value)
    {
        SaveCommand.NotifyCanExecuteChanged();
    }

    private static bool TryParseThickness(string? text, out double value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var trimmed = text.Trim();
        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return true;
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

public sealed record PlantMeasurementEditResult(string Label, double Thickness, string? Note);
