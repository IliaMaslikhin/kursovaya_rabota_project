using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OilErp.Bootstrap;
using OilErp.Core.Contracts;
using OilErp.Core.Services.Central;

namespace OilErp.Ui.ViewModels;

public sealed partial class AnalyticsPanelViewModel : ObservableObject
{
    private readonly IStoragePort storage;

    public AnalyticsPanelViewModel(IStoragePort storage)
    {
        this.storage = storage;
        Items = new ObservableCollection<AnalyticsRowViewModel>();
        statusMessage = "Нажмите «Обновить» для загрузки.";
    }

    public ObservableCollection<AnalyticsRowViewModel> Items { get; }

    [ObservableProperty] private string statusMessage;
    [ObservableProperty] private bool isBusy;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    public async Task IngestAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            IsBusy = true;
            StatusMessage = "Запускаем сбор данных из заводов...";
            var ingest = new FnIngestEventsService(storage);
            var affected = await ingest.fn_ingest_eventsAsync(5000, CancellationToken.None);
            StatusMessage = $"Сбор завершён, обработано {affected} событий. Обновляем аналитику...";
            AppLogger.Info($"[ui] ingest events завершён, обработано {affected} за {sw.ElapsedMilliseconds} мс");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сбора: {ex.Message}";
            AppLogger.Error($"[ui] ingest ошибка: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            IsBusy = true;
            StatusMessage = "Загружаем аналитику из central...";
            Items.Clear();
            var topService = new FnTopAssetsByCrService(storage);
            var summaryService = new FnAssetSummaryJsonService(storage);
            AppLogger.Info("[ui] analytics load start");
            var rows = await topService.fn_top_assets_by_crAsync(100, CancellationToken.None);

            foreach (var row in rows)
            {
                var asset = row.AssetCode ?? "UNKNOWN";
                var cr = row.Cr;
                var updatedAt = row.UpdatedAt;

                string? risk = null;
                string plant = "CENTRAL";
                try
                {
                    var summary = await summaryService.fn_asset_summary_jsonAsync(asset, "default", CancellationToken.None);
                    plant = summary?.Asset.PlantCode ?? plant;
                    risk = summary?.Risk?.Level;
                }
                catch
                {
                    risk = null;
                }

                Items.Add(new AnalyticsRowViewModel(
                    asset,
                    plant,
                    cr?.ToString("0.0000") ?? "—",
                    risk ?? "—",
                    updatedAt?.ToString("u") ?? "—"));
            }

            StatusMessage = $"Загружено {Items.Count} строк.";
            AppLogger.Info($"[ui] аналитика загружена rows={Items.Count} durMs={sw.ElapsedMilliseconds}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки аналитики: {ex.Message}";
            AppLogger.Error($"[ui] аналитика ошибка: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string? Read(IReadOnlyDictionary<string, object?> row, string name)
    {
        if (row.TryGetValue(name, out var value) && value != null) return value.ToString();
        var kvp = row.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase));
        return kvp.Value?.ToString();
    }

}

public sealed record AnalyticsRowViewModel(string AssetCode, string Plant, string CrDisplay, string Risk, string UpdatedAt);
