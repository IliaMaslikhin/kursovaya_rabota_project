using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OilErp.Core.Contracts;
using OilErp.Core.Dto;
using OilErp.Core.Operations;

namespace OilErp.Ui.ViewModels;

public sealed class OperationOption
{
    public OperationOption(OperationKind kind, string title, string description, string icon)
    {
        Kind = kind;
        Title = title;
        Description = description;
        Icon = icon;
    }

    public OperationKind Kind { get; }
    public string Title { get; }
    public string Description { get; }
    public string Icon { get; }
}

public enum OperationKind
{
    AssetUpsert,
    PolicyUpsert,
    EventsEnqueue
}

public sealed partial class AddOperationFormViewModel : ObservableObject
{
    private readonly IStoragePort storage;

    public AddOperationFormViewModel(IStoragePort storage)
    {
        this.storage = storage;
        OperationKinds = new[]
        {
            new OperationOption(OperationKind.AssetUpsert, "Актив", "Создать/обновить актив в центральной БД.", "🏭"),
            new OperationOption(OperationKind.PolicyUpsert, "Политика", "Настроить пороги коррозии.", "⚖"),
            new OperationOption(OperationKind.EventsEnqueue, "Событие", "Отправить событие инжесту.", "✉")
        };
        selectedOperation = OperationKinds.First();

        assetPlant = "ANPZ";
        policyName = "default";
        policyLow = 0.001m;
        policyMed = 0.005m;
        policyHigh = 0.01m;
        eventType = "UI_MANUAL_EVENT";
        eventPlant = "ANPZ";
        eventPayload = "{\"message\":\"hello\"}";
        operationStatus = "Выберите операцию и заполните параметры.";
        UpdateOperationFlags(selectedOperation);
    }

    public IReadOnlyList<OperationOption> OperationKinds { get; }

    [ObservableProperty]
    private OperationOption selectedOperation;

    [ObservableProperty]
    private string assetCode = string.Empty;

    [ObservableProperty]
    private string assetName = string.Empty;

    [ObservableProperty]
    private string assetType = "PROCESS_UNIT";

    [ObservableProperty]
    private string assetPlant;

    [ObservableProperty]
    private string policyName;

    [ObservableProperty]
    private decimal policyLow;

    [ObservableProperty]
    private decimal policyMed;

    [ObservableProperty]
    private decimal policyHigh;

    [ObservableProperty]
    private string eventType;

    [ObservableProperty]
    private string eventPlant;

    [ObservableProperty]
    private string eventPayload;

    [ObservableProperty]
    private string operationStatus;

    [ObservableProperty]
    private bool isAssetOperation;

    [ObservableProperty]
    private bool isPolicyOperation;

    [ObservableProperty]
    private bool isEventOperation;

    partial void OnSelectedOperationChanged(OperationOption value)
    {
        UpdateOperationFlags(value);
    }

    [RelayCommand]
    private async Task SubmitOperationAsync()
    {
        if (SelectedOperation is null)
        {
            OperationStatus = "Выберите тип операции.";
            return;
        }

        try
        {
            int affected;
            switch (SelectedOperation.Kind)
            {
                case OperationKind.AssetUpsert:
                    affected = await ExecuteAssetUpsertAsync();
                    break;
                case OperationKind.PolicyUpsert:
                    affected = await ExecutePolicyUpsertAsync();
                    break;
                case OperationKind.EventsEnqueue:
                    affected = await ExecuteEventEnqueueAsync();
                    break;
                default:
                    OperationStatus = "Неизвестная операция.";
                    return;
            }

            OperationStatus = $"Команда '{SelectedOperation.Title}' выполнена (rows={affected}).";
        }
        catch (Exception ex)
        {
            OperationStatus = $"Ошибка выполнения: {ex.Message}";
        }
    }

    private async Task<int> ExecuteAssetUpsertAsync()
    {
        if (string.IsNullOrWhiteSpace(AssetCode) || string.IsNullOrWhiteSpace(AssetPlant))
        {
            throw new InvalidOperationException("Укажите код актива и завод.");
        }

        var spec = new CommandSpec(
            OperationNames.Central.AssetUpsert,
            new Dictionary<string, object?>
            {
                ["p_asset_code"] = AssetCode.Trim(),
                ["p_name"] = string.IsNullOrWhiteSpace(AssetName) ? null : AssetName.Trim(),
                ["p_type"] = string.IsNullOrWhiteSpace(AssetType) ? null : AssetType.Trim(),
                ["p_plant_code"] = AssetPlant.Trim()
            });

        return await storage.ExecuteCommandAsync(spec);
    }

    private async Task<int> ExecutePolicyUpsertAsync()
    {
        if (string.IsNullOrWhiteSpace(PolicyName))
        {
            throw new InvalidOperationException("Имя политики обязательно.");
        }

        var spec = new CommandSpec(
            OperationNames.Central.PolicyUpsert,
            new Dictionary<string, object?>
            {
                ["p_name"] = PolicyName.Trim(),
                ["p_low"] = PolicyLow,
                ["p_med"] = PolicyMed,
                ["p_high"] = PolicyHigh
            });
        return await storage.ExecuteCommandAsync(spec);
    }

    private async Task<int> ExecuteEventEnqueueAsync()
    {
        if (string.IsNullOrWhiteSpace(EventType) || string.IsNullOrWhiteSpace(EventPlant))
        {
            throw new InvalidOperationException("Укажите тип события и завод.");
        }

        var payload = string.IsNullOrWhiteSpace(EventPayload) ? "{}" : EventPayload.Trim();
        ValidateJson(payload);

        var spec = new CommandSpec(
            OperationNames.Central.EventsEnqueue,
            new Dictionary<string, object?>
            {
                ["p_event_type"] = EventType.Trim(),
                ["p_source_plant"] = EventPlant.Trim(),
                ["p_payload"] = payload
            });

        return await storage.ExecuteCommandAsync(spec);
    }

    private static void ValidateJson(string payload)
    {
        try
        {
            JsonDocument.Parse(payload);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Некорректный JSON: {ex.Message}");
        }
    }

    private void UpdateOperationFlags(OperationOption? option)
    {
        IsAssetOperation = option?.Kind == OperationKind.AssetUpsert;
        IsPolicyOperation = option?.Kind == OperationKind.PolicyUpsert;
        IsEventOperation = option?.Kind == OperationKind.EventsEnqueue;
    }
}
