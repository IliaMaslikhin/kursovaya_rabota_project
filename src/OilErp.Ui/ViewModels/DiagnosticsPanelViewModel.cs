using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OilErp.Bootstrap;
using OilErp.Core.Dto;
using OilErp.Ui.Services;

namespace OilErp.Ui.ViewModels;

public sealed partial class DiagnosticsPanelViewModel : ObservableObject
{
    private readonly StoragePortFactory factory;
    private CancellationTokenSource? listenCts;

    public DiagnosticsPanelViewModel(StoragePortFactory factory)
    {
        this.factory = factory;
        Entries = new ObservableCollection<DiagnosticEntryViewModel>();
        AvailableChannels = new ObservableCollection<string>(new[]
        {
            "events_ingest",
            "events_enqueue",
            "events_requeue",
            "events_cleanup"
        });
        channel = AvailableChannels.First();
        autoReconnect = true;
        statusMessage = "Укажите канал LISTEN и нажмите «Старт».";
    }

    public ObservableCollection<DiagnosticEntryViewModel> Entries { get; }
    public ObservableCollection<string> AvailableChannels { get; }

    [ObservableProperty] private string channel;
    [ObservableProperty] private bool isListening;
    [ObservableProperty] private string statusMessage;
    [ObservableProperty] private bool autoReconnect;

    [RelayCommand]
    public async Task StartAsync()
    {
        if (IsListening) return;
        var chan = Channel?.Trim();
        if (string.IsNullOrWhiteSpace(chan))
        {
            StatusMessage = "Укажите имя канала.";
            return;
        }

        try
        {
            listenCts = new CancellationTokenSource();
            var storage = factory.Central;
            storage.Notified += OnNotified;
            await storage.SubscribeAsync(chan, listenCts.Token);
            IsListening = true;
            StatusMessage = $"Слушаем {chan}";
            AppLogger.Info($"[ui] diagnostics listening on {chan}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка подписки: {ex.Message}";
            StopInternal();
        }
    }

    [RelayCommand]
    public async Task StopAsync()
    {
        StopInternal();
        var chan = Channel?.Trim();
        if (string.IsNullOrWhiteSpace(chan)) return;
        try
        {
            await factory.Central.UnsubscribeAsync(chan);
        }
        catch
        {
            // ignore
        }
    }

    private void StopInternal()
    {
        if (!IsListening) return;
        try
        {
            factory.Central.Notified -= OnNotified;
            listenCts?.Cancel();
            listenCts?.Dispose();
        }
        finally
        {
            IsListening = false;
            StatusMessage = "Остановлено.";
        }
    }

    private void OnNotified(object? sender, DbNotification notification)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss");
        Entries.Insert(0, new DiagnosticEntryViewModel(ts, notification.Channel, notification.Payload));
        while (Entries.Count > 100)
        {
            Entries.RemoveAt(Entries.Count - 1);
        }

        if (!AutoReconnect || IsListening) return;
        // Если слушатель упал и перестал быть отмечен, попробуем авто-старт
        _ = StartAsync();
    }
}
