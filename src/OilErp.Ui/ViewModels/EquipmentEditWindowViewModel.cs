using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OilErp.Ui.ViewModels;

public sealed partial class EquipmentEditWindowViewModel : ObservableObject
{
    public EquipmentEditWindowViewModel(
        string title,
        string codeLabel,
        string code,
        bool isCodeReadOnly,
        string field1Label,
        string? field1,
        string field2Label,
        string? field2,
        IReadOnlyList<string>? field2Options = null)
    {
        Title = title;
        CodeLabel = codeLabel;
        Code = code;
        IsCodeReadOnly = isCodeReadOnly;
        Field1Label = field1Label;
        Field1 = field1 ?? string.Empty;
        Field2Label = field2Label;
        Field2 = field2 ?? string.Empty;
        Field2Options = field2Options ?? Array.Empty<string>();
        statusMessage = string.Empty;
    }

    public string Title { get; }

    public string CodeLabel { get; }

    [ObservableProperty] private string code;

    public bool IsCodeReadOnly { get; }

    public string Field1Label { get; }

    [ObservableProperty] private string field1;

    public string Field2Label { get; }

    [ObservableProperty] private string field2;

    public IReadOnlyList<string> Field2Options { get; }

    public bool HasField2Options => Field2Options.Count > 0;

    public bool ShowPlantMetaHint =>
        string.Equals(Field1Label, "Локация", StringComparison.OrdinalIgnoreCase)
        && string.Equals(Field2Label, "Статус", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty] private string statusMessage;

    public event Action<EquipmentEditResult?>? RequestClose;

    private bool CanSave() => !string.IsNullOrWhiteSpace(Code);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Code))
        {
            StatusMessage = "Укажите код.";
            SaveCommand.NotifyCanExecuteChanged();
            return;
        }

        var result = new EquipmentEditResult(
            Code.Trim(),
            string.IsNullOrWhiteSpace(Field1) ? null : Field1.Trim(),
            string.IsNullOrWhiteSpace(Field2) ? null : Field2.Trim());
        RequestClose?.Invoke(result);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(null);
    }

    partial void OnCodeChanged(string value)
    {
        SaveCommand.NotifyCanExecuteChanged();
    }
}

public sealed record EquipmentEditResult(string Code, string? Field1, string? Field2);
