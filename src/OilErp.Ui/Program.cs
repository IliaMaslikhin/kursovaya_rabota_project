using System;
using Avalonia;
using OilErp.Bootstrap;
using OilErp.Tests.Runner;

namespace OilErp.Ui;

sealed class Program
{
    // Инициализация. Не трогаем Avalonia/другие API до AppMain — окружение ещё не поднято.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // Перед запуском UI прогоняем смоук-тесты.
            var smoke = SmokeSuite.RunAsync().GetAwaiter().GetResult();
            if (!smoke.Success)
            {
                AppLogger.Error($"[ui] смоук-тесты не прошли: {smoke.Summary}");
                Console.Error.WriteLine("Смоук-тесты не прошли, UI не запущен.");
                Console.Error.WriteLine(smoke.Summary);
                Environment.ExitCode = 1;
                return;
            }

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

    // Настройка Avalonia, нужна и приложению, и дизайнеру.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
