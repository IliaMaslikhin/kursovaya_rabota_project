using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Services.Central;

namespace OilErp.Ui.ViewModels;

public sealed partial class EventQueueViewModel : ObservableObject
{
    private readonly IStoragePort storage;
    private bool ingestSubscribed;

    public EventQueueViewModel(IStoragePort storage)
    {
        this.storage = storage;
        Events = new ObservableCollection<EventQueueRowViewModel>();
        IngestNotifications = new ObservableCollection<string>();
        statusMessage = "Нажмите «Обновить» для чтения очереди.";
        limit = "50";
        cleanupAgeSeconds = "3600";
        SubscribeToIngestNotifications();
    }

    public ObservableCollection<EventQueueRowViewModel> Events { get; }
    public ObservableCollection<string> IngestNotifications { get; }

    [ObservableProperty] private string statusMessage;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string limit;
    [ObservableProperty] private string cleanupAgeSeconds;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (!int.TryParse(Limit, out var parsedLimit) || parsedLimit <= 0) parsedLimit = 50;
        try
        {
            IsBusy = true;
            StatusMessage = "Читаем очередь...";
            Events.Clear();
            var svc = new FnEventsPeekService(storage);
            var rows = await svc.fn_events_peekAsync(parsedLimit, CancellationToken.None);
            foreach (var row in rows)
            {
                var id = row.Id;
                var type = row.EventType ?? "n/a";
                var plant = row.SourcePlant ?? "n/a";
                var created = row.CreatedAt;
                var payload = row.PayloadJson;
                Events.Add(new EventQueueRowViewModel(id, type, plant, created, payload));
            }
            StatusMessage = $"Непроцессed: {Events.Count}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка чтения: {ex.Message}";
            AppLogger.Error($"[ui] queue refresh error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RequeueAsync()
    {
        if (Events.Count == 0)
        {
            StatusMessage = "Сначала обновите очередь.";
            return;
        }

        try
        {
            IsBusy = true;
            var ids = Events.Select(e => e.Id).ToArray();
            var svc = new FnEventsRequeueService(storage);
            var affected = await svc.fn_events_requeueAsync(ids, CancellationToken.None);
            StatusMessage = $"Перезагружено: {affected}";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка requeue: {ex.Message}";
            AppLogger.Error($"[ui] queue requeue error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task CleanupAsync()
    {
        var age = TimeSpan.FromSeconds(3600);
        if (int.TryParse(CleanupAgeSeconds, out var ageSec) && ageSec > 0)
        {
            age = TimeSpan.FromSeconds(ageSec);
        }

        try
        {
            IsBusy = true;
            var svc = new FnEventsCleanupService(storage);
            var affected = await svc.fn_events_cleanupAsync(age, CancellationToken.None);
            StatusMessage = $"Удалено {affected} событий старше {age}.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка cleanup: {ex.Message}";
            AppLogger.Error($"[ui] queue cleanup error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SubscribeToIngestNotifications()
    {
        if (ingestSubscribed) return;
        ingestSubscribed = true;
        storage.Notified += OnNotified;
        _ = storage.SubscribeAsync("events_ingest");
    }

    private void OnNotified(object? sender, DbNotification notification)
    {
        if (!string.Equals(notification.Channel, "events_ingest", StringComparison.Ordinal)) return;
        var text = notification.Payload ?? string.Empty;
        try
        {
            var doc = JsonDocument.Parse(text);
            var processed = doc.RootElement.TryGetProperty("processed", out var p) ? p.GetInt32() : 0;
            var ts = doc.RootElement.TryGetProperty("ts", out var t) ? t.GetString() : DateTime.UtcNow.ToString("O");
            text = $"processed={processed} at {ts}";
        }
        catch
        {
            // leave raw payload
        }

        IngestNotifications.Insert(0, text);
        while (IngestNotifications.Count > 20)
        {
            IngestNotifications.RemoveAt(IngestNotifications.Count - 1);
        }
    }
}

public sealed record EventQueueRowViewModel(long Id, string EventType, string SourcePlant, DateTime? CreatedAt, string Payload);
