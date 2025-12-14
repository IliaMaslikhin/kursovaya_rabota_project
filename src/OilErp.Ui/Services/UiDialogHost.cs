using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace OilErp.Ui.Services;

public static class UiDialogHost
{
    public static Window? TryGetOwner()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
        }

        return null;
    }

    public static Task<TResult?> ShowDialogAsync<TResult>(Window dialog)
    {
        var owner = TryGetOwner();
        if (owner is null)
        {
            dialog.Show();
            return Task.FromResult<TResult?>(default);
        }

        return dialog.ShowDialog<TResult?>(owner);
    }
}

