using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

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

public static class UiFilePicker
{
    public static FilePickerFileType CsvFileType { get; } = new("CSV")
    {
        Patterns = new[] { "*.csv" }
    };

    public static FilePickerFileType JsonFileType { get; } = new("JSON")
    {
        Patterns = new[] { "*.json" }
    };

    public static FilePickerFileType XlsxFileType { get; } = new("Excel (.xlsx)")
    {
        Patterns = new[] { "*.xlsx" }
    };

    public static async Task<bool> SaveTextAsync(
        string title,
        string suggestedFileName,
        string content,
        params FilePickerFileType[] fileTypes)
    {
        var owner = UiDialogHost.TryGetOwner();
        if (owner?.StorageProvider is null) return false;

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = fileTypes?.ToList()
        });

        if (file is null) return false;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content);
        await writer.FlushAsync();
        return true;
    }

    public static async Task<bool> SaveBytesAsync(
        string title,
        string suggestedFileName,
        byte[] content,
        params FilePickerFileType[] fileTypes)
    {
        var owner = UiDialogHost.TryGetOwner();
        if (owner?.StorageProvider is null) return false;

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = fileTypes?.ToList()
        });

        if (file is null) return false;

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(content);
        await stream.FlushAsync();
        return true;
    }

    public static async Task<(string? FileName, string? Content)> OpenTextAsync(
        string title,
        params FilePickerFileType[] fileTypes)
    {
        var owner = UiDialogHost.TryGetOwner();
        if (owner?.StorageProvider is null) return default;

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes?.ToList()
        });

        var file = files?.FirstOrDefault();
        if (file is null) return default;

        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        return (file.Name, content);
    }
}
