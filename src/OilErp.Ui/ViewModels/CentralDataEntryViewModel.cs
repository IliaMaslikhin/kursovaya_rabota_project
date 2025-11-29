using System;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OilErp.Bootstrap;
using OilErp.Core.Services.Central;
using OilErp.Core.Contracts;

namespace OilErp.Ui.ViewModels;

public sealed partial class CentralDataEntryViewModel : ObservableObject
{
    private readonly IStoragePort storage;

    public CentralDataEntryViewModel(IStoragePort storage)
    {
        this.storage = storage;
        assetStatus = "Готовы к записи актива.";
        policyStatus = "Готовы к записи политики.";
        eventStatus = "Готовы к отправке события.";
        assetPlant = "ANPZ";
        assetType = "PROCESS_UNIT";
        policyName = "default";
        policyLow = 0.001m;
        policyMed = 0.005m;
        policyHigh = 0.01m;
        eventType = "UI_EVENT";
        eventPlant = "ANPZ";
        eventPayload = "{\"message\":\"ping\"}";
    }

    [ObservableProperty] private string assetCode = string.Empty;
    [ObservableProperty] private string assetName = string.Empty;
    [ObservableProperty] private string assetType;
    [ObservableProperty] private string assetPlant;
    [ObservableProperty] private string assetStatus;

    [ObservableProperty] private string policyName;
    [ObservableProperty] private decimal policyLow;
    [ObservableProperty] private decimal policyMed;
    [ObservableProperty] private decimal policyHigh;
    [ObservableProperty] private string policyStatus;

    [ObservableProperty] private string eventType;
    [ObservableProperty] private string eventPlant;
    [ObservableProperty] private string eventPayload;
    [ObservableProperty] private string eventStatus;

    [RelayCommand]
    private async Task SaveAssetAsync()
    {
        try
        {
            AppLogger.Info($"[ui] сохранение актива code={AssetCode} plant={AssetPlant}");
            var service = new FnAssetUpsertService(storage);
            var rows = await service.fn_asset_upsertAsync(
                AssetCode.Trim(),
                string.IsNullOrWhiteSpace(AssetName) ? null : AssetName.Trim(),
                string.IsNullOrWhiteSpace(AssetType) ? null : AssetType.Trim(),
                string.IsNullOrWhiteSpace(AssetPlant) ? null : AssetPlant.Trim().ToUpperInvariant());
            AssetStatus = $"Сохранено (строк={rows}).";
            AppLogger.Info($"[ui] актив сохранен code={AssetCode} rows={rows}");
        }
        catch (Exception ex)
        {
            AssetStatus = $"Ошибка сохранения: {ex.Message}";
            AppLogger.Error($"[ui] ошибка сохранения актива code={AssetCode}: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SavePolicyAsync()
    {
        try
        {
            AppLogger.Info($"[ui] сохранение политики name={PolicyName}");
            var service = new FnPolicyUpsertService(storage);
            var rows = await service.fn_policy_upsertAsync(
                PolicyName.Trim(),
                PolicyLow,
                PolicyMed,
                PolicyHigh);
            PolicyStatus = $"Сохранено (строк={rows}).";
            AppLogger.Info($"[ui] политика сохранена name={PolicyName} rows={rows}");
        }
        catch (Exception ex)
        {
            PolicyStatus = $"Ошибка политики: {ex.Message}";
            AppLogger.Error($"[ui] ошибка политики name={PolicyName}: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task EnqueueEventAsync()
    {
        try
        {
            JsonDocument.Parse(EventPayload); // validate
            AppLogger.Info($"[ui] отправка события type={EventType} plant={EventPlant}");
            var service = new FnEventsEnqueueService(storage);
            var rows = await service.fn_events_enqueueAsync(
                EventType.Trim(),
                EventPlant.Trim().ToUpperInvariant(),
                EventPayload.Trim());
            EventStatus = $"Отправлено (строк={rows}).";
            AppLogger.Info($"[ui] событие отправлено type={EventType} rows={rows}");
        }
        catch (JsonException jex)
        {
            EventStatus = $"Некорректный JSON: {jex.Message}";
            AppLogger.Error($"[ui] payload json error: {jex.Message}");
        }
        catch (Exception ex)
        {
            EventStatus = $"Ошибка отправки: {ex.Message}";
            AppLogger.Error($"[ui] ошибка отправки события type={EventType}: {ex.Message}");
        }
    }
}
