using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OilErp.Ui.ViewModels;

public sealed partial class RiskPolicyEditWindowViewModel : ObservableObject
{
    public RiskPolicyEditWindowViewModel(
        string title,
        string name,
        bool isNameReadOnly,
        decimal low,
        decimal med,
        decimal high)
    {
        Title = title;
        Name = name;
        IsNameReadOnly = isNameReadOnly;
        this.low = low;
        this.med = med;
        this.high = high;
        statusMessage = string.Empty;
    }

    public string Title { get; }

    [ObservableProperty] private string name;

    public bool IsNameReadOnly { get; }

    [ObservableProperty] private decimal low;

    [ObservableProperty] private decimal med;

    [ObservableProperty] private decimal high;

    [ObservableProperty] private string statusMessage;

    public event Action<RiskPolicyEditResult?>? RequestClose;

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = "Укажите имя политики.";
            SaveCommand.NotifyCanExecuteChanged();
            return;
        }

        RequestClose?.Invoke(new RiskPolicyEditResult(Name.Trim(), Low, Med, High));
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(null);
    }

    partial void OnNameChanged(string value)
    {
        SaveCommand.NotifyCanExecuteChanged();
    }
}

public sealed record RiskPolicyEditResult(string Name, decimal Low, decimal Med, decimal High);

