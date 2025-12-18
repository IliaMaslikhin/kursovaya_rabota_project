using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OilErp.Ui.ViewModels;

public sealed partial class CentralMeasurementEditWindowViewModel : ObservableObject
{
    public CentralMeasurementEditWindowViewModel(
        string title,
        string equipmentCode,
        string? initialLabel = null,
        double? initialThickness = null,
        string? initialNote = null,
        DateTime? initialDateLocal = null,
        bool isLabelReadOnly = false)
    {
        Title = title;
        EquipmentCode = equipmentCode;

        dateText = (initialDateLocal ?? DateTime.Now.Date).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        label = string.IsNullOrWhiteSpace(initialLabel) ? "T1" : initialLabel.Trim();
        thicknessText = (initialThickness ?? 12.0).ToString("0.###", CultureInfo.InvariantCulture);
        note = string.IsNullOrWhiteSpace(initialNote) ? null : initialNote.Trim();
        IsLabelReadOnly = isLabelReadOnly;
        statusMessage = string.Empty;
    }

    public string Title { get; }

    public string EquipmentCode { get; }

    [ObservableProperty] private string dateText;

    [ObservableProperty] private string label;

    [ObservableProperty] private string thicknessText;

    [ObservableProperty] private string? note;

    [ObservableProperty] private string statusMessage;

    public bool IsLabelReadOnly { get; }

    public event Action<CentralMeasurementEditResult?>? RequestClose;

    private bool CanSave() =>
        !string.IsNullOrWhiteSpace(Label)
        && TryParseLocalDate(DateText, out _)
        && TryParseThickness(ThicknessText, out var thk)
        && thk > 0;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            StatusMessage = "Укажите метку точки (label).";
            SaveCommand.NotifyCanExecuteChanged();
            return;
        }

        if (!TryParseLocalDate(DateText, out var dateLocal))
        {
            StatusMessage = "Дата должна быть в формате dd.MM.yyyy (например: 18.12.2025).";
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

        var result = new CentralMeasurementEditResult(
            dateLocal,
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

    partial void OnDateTextChanged(string value)
    {
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnLabelChanged(string value)
    {
        SaveCommand.NotifyCanExecuteChanged();
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

    private static bool TryParseLocalDate(string? text, out DateTime dateLocal)
    {
        dateLocal = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var trimmed = text.Trim();

        if (DateTime.TryParseExact(trimmed, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ddmmyyyy))
        {
            dateLocal = ddmmyyyy.Date;
            return true;
        }

        if (DateTime.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
        {
            dateLocal = iso.Date;
            return true;
        }

        if (DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed))
        {
            dateLocal = parsed.Date;
            return true;
        }

        return false;
    }
}

public sealed record CentralMeasurementEditResult(DateTime DateLocal, string Label, double Thickness, string? Note);
