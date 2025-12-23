using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npgsql;
using OilErp.Bootstrap;
using OilErp.Core.Dto;
using OilErp.Ui.Services;
using OilErp.Ui.Views;

namespace OilErp.Ui.ViewModels;

public sealed partial class ConnectWindowViewModel : ViewModelBase
{
    private readonly IClassicDesktopStyleApplicationLifetime? desktop;

    public ConnectWindowViewModel() : this(null) { }

    public ConnectWindowViewModel(IClassicDesktopStyleApplicationLifetime? desktop)
    {
        this.desktop = desktop;
        ConnectionForm = new ConnectionFormViewModel(ConnectAsync, ReadDefaultConnection());
    }

    public ConnectionFormViewModel ConnectionForm { get; }

    public event Action? RequestClose;

    private async Task ConnectAsync(DatabaseProfile profile, string connectionString)
    {
        var gateway = await Task.Run(() => KernelGateway.Create(connectionString, profile));
        if (desktop != null)
        {
            var effectiveConn = gateway.StorageConfig?.ConnectionString ?? connectionString;
            var mainVm = new MainWindowViewModel(gateway, profile, effectiveConn);
            var main = new MainWindow { DataContext = mainVm };
            main.Show();
            desktop.MainWindow = main;
            RequestClose?.Invoke();
        }
    }

    private static string? ReadDefaultConnection()
    {
        return Environment.GetEnvironmentVariable("OILERP__DB__CONN")
               ?? Environment.GetEnvironmentVariable("OIL_ERP_PG");
    }
}

public sealed partial class ConnectionFormViewModel : ObservableObject
{
    private readonly Func<DatabaseProfile, string, Task> connectAction;

    public ConnectionFormViewModel(Func<DatabaseProfile, string, Task> connectAction, string? defaultConnection = null)
    {
        this.connectAction = connectAction;
        connectionString = defaultConnection ?? string.Empty;
        host = "localhost";
        port = 5432;
        selectedProfile = DatabaseProfile.Central;
        database = ResolveDatabaseByProfile(selectedProfile);
        username = "postgres";
        password = "postgres";
        timeoutSec = 30;
        statusMessage = string.IsNullOrWhiteSpace(defaultConnection)
            ? "Введите строку подключения или соберите её из полей ниже."
            : "Найдена строка подключения из окружения.";
        useCustomString = !string.IsNullOrWhiteSpace(defaultConnection);

        if (!string.IsNullOrWhiteSpace(defaultConnection))
        {
            TryApplyDefaultsFromConnectionString(defaultConnection);
        }
    }

    public IReadOnlyList<DatabaseProfile> Profiles { get; } = new[]
    {
        DatabaseProfile.Central,
        DatabaseProfile.PlantAnpz,
        DatabaseProfile.PlantKrnpz
    };

    [ObservableProperty] private DatabaseProfile selectedProfile;
    [ObservableProperty] private bool useCustomString;
    [ObservableProperty] private string connectionString;
    [ObservableProperty] private string host;
    [ObservableProperty] private int port;
    [ObservableProperty] private string database;
    [ObservableProperty] private string username;
    [ObservableProperty] private string password;
    [ObservableProperty] private int timeoutSec;
    [ObservableProperty] private string statusMessage;
    [ObservableProperty] private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
    }

    partial void OnSelectedProfileChanged(DatabaseProfile value)
    {
        if (UseCustomString) return;
        Database = ResolveDatabaseByProfile(value);
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Пробуем подключиться...";
            var conn = BuildConnectionString();
            AppLogger.Info($"[ui] пользователь инициировал подключение conn='{conn}'");
            await connectAction(SelectedProfile, conn);
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

    [RelayCommand]
    private async Task TestAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Проверяем подключение...";
            var connStr = BuildConnectionString();
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "select current_database()";
            var db = (await cmd.ExecuteScalarAsync())?.ToString() ?? "?";
            StatusMessage = $"Успешно: подключение установлено (db={db}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка проверки: {ex.Message}";
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

        var db = string.IsNullOrWhiteSpace(Database) ? ResolveDatabaseByProfile(SelectedProfile) : Database.Trim();
        return $"Host={Host};Port={Port};Username={Username};Password={Password};Database={db};Timeout={TimeoutSec}";
    }

    private static string ResolveDatabaseByProfile(DatabaseProfile profile)
    {
        return profile switch
        {
            DatabaseProfile.Central => "central",
            DatabaseProfile.PlantAnpz => "anpz",
            DatabaseProfile.PlantKrnpz => "krnpz",
            _ => "central"
        };
    }

    private void TryApplyDefaultsFromConnectionString(string connectionStringValue)
    {
        try
        {
            var b = new NpgsqlConnectionStringBuilder(connectionStringValue);
            if (!string.IsNullOrWhiteSpace(b.Host)) Host = b.Host;
            if (b.Port > 0) Port = b.Port;
            if (!string.IsNullOrWhiteSpace(b.Username)) Username = b.Username;
            if (!string.IsNullOrWhiteSpace(b.Password)) Password = b.Password;
            if (b.Timeout > 0) TimeoutSec = (int)b.Timeout;

            var db = b.Database?.ToLowerInvariant();
            SelectedProfile = db switch
            {
                var s when s != null && s.Contains("anpz") => DatabaseProfile.PlantAnpz,
                var s when s != null && s.Contains("krnpz") => DatabaseProfile.PlantKrnpz,
                _ => DatabaseProfile.Central
            };
            Database = ResolveDatabaseByProfile(SelectedProfile);
        }
        catch
        {
            // игнорируем и оставляем значения по умолчанию
        }
    }
}
