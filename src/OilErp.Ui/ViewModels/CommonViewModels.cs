using System;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OilErp.Ui.ViewModels;

public abstract class ViewModelBase : ObservableObject;

public sealed record ThemeOption(string Code, string Title, ThemePalette Palette, ThemeVariant Variant)
{
    public override string ToString() => Title;
}

public sealed partial class ConfirmDialogViewModel : ObservableObject
{
    public ConfirmDialogViewModel(string title, string message, string confirmText = "Да", string cancelText = "Отмена")
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
    }

    public string Title { get; }
    public string Message { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }

    public event Action<bool?>? RequestClose;

    [RelayCommand]
    private void Confirm() => RequestClose?.Invoke(true);

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);
}
