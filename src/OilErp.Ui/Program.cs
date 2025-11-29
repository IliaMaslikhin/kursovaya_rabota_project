using System;
using Avalonia;
using OilErp.Bootstrap;

namespace OilErp.Ui;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[ui] fatal startup: {ex.Message}");
            Console.Error.WriteLine("Ошибка старта UI: " + ex.Message);
            Console.Error.WriteLine("Установите переменные OIL_ERP_PG или OILERP__DB__CONN с валидной строкой подключения к PostgreSQL.");
            Environment.ExitCode = 1;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
