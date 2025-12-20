using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OilErp.Ui.ViewModels;

/// <summary>
/// Один блок аналитики по конкретному заводу.
/// </summary>
public sealed partial class AnalyticsPlantGroupViewModel : ObservableObject
{
    private readonly Func<AnalyticsPlantGroupViewModel, Task> refreshAction;
    private readonly Func<AnalyticsPlantGroupViewModel, Task> applyPolicyAction;

    public AnalyticsPlantGroupViewModel(
        string plantCode,
        string title,
        string[] plantAliases,
        bool includeNullPlantCode,
        ObservableCollection<string> policyNames,
        Func<AnalyticsPlantGroupViewModel, Task> refreshAction,
        Func<AnalyticsPlantGroupViewModel, Task> applyPolicyAction)
    {
        PlantCode = plantCode;
        Title = title;
        PlantAliases = plantAliases;
        IncludeNullPlantCode = includeNullPlantCode;
        PolicyNames = policyNames;
        this.refreshAction = refreshAction;
        this.applyPolicyAction = applyPolicyAction;

        Items = new ObservableCollection<AnalyticsRowViewModel>();
        selectedPolicyName = "default";
        statusMessage = "Нажмите «Обновить»";
        summaryText = string.Empty;
    }

    public string PlantCode { get; }

    public string Title { get; }

    public string[] PlantAliases { get; }

    // Если true — в этот блок попадут активы, у которых plant_code = NULL.
    // Это удобно для "Central", когда активы заводом не помечены.
    public bool IncludeNullPlantCode { get; }

    public ObservableCollection<string> PolicyNames { get; }

    public ObservableCollection<AnalyticsRowViewModel> Items { get; }

    [ObservableProperty] private string selectedPolicyName;

    [ObservableProperty] private string statusMessage;

    [ObservableProperty] private string summaryText;

    [ObservableProperty] private bool isBusy;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await refreshAction(this);
    }

    [RelayCommand]
    private async Task ApplyPolicyAsync()
    {
        await applyPolicyAction(this);
    }
}
