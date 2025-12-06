using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OilErp.Bootstrap;

namespace OilErp.Ui.ViewModels;

public sealed partial class ConnectionFormViewModel : ObservableObject
{
    private readonly Func<string, Task> connectAction;

    public ConnectionFormViewModel(Func<string, Task> connectAction, string? defaultConnection = null)
    {
        this.connectAction = connectAction;
        connectionString = defaultConnection ?? string.Empty;
        host = "localhost";
        database = "central";
        username = "postgres";
        password = "postgres";
        timeoutSec = 30;
        statusMessage = string.IsNullOrWhiteSpace(defaultConnection)
            ? "Введите строку подключения или соберите её из полей ниже."
            : "Найдена строка подключения из окружения.";
        useCustomString = !string.IsNullOrWhiteSpace(defaultConnection);
    }

    [ObservableProperty] private bool useCustomString;
    [ObservableProperty] private string connectionString;
    [ObservableProperty] private string host;
    [ObservableProperty] private string database;
    [ObservableProperty] private string username;
    [ObservableProperty] private string password;
    [ObservableProperty] private int timeoutSec;
    [ObservableProperty] private string statusMessage;
    [ObservableProperty] private bool isBusy;

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Пробуем подключиться...";
            var conn = BuildConnectionString();
            AppLogger.Info($"[ui] пользователь инициировал подключение conn='{conn}'");
            await connectAction(conn);
            StatusMessage = "Подключение установлено.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
            AppLogger.Error($"[ui] ошибка подключения: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string BuildConnectionString()
    {
        if (UseCustomString && !string.IsNullOrWhiteSpace(ConnectionString))
        {
            return ConnectionString.Trim();
        }

        return $"Host={Host};Username={Username};Password={Password};Database={Database};Timeout={TimeoutSec}";
    }
}
