using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npgsql;
using NpgsqlTypes;
using OilErp.Ui.Services;

namespace OilErp.Ui.ViewModels;

/// <summary>
/// Аналитика по активам: по умолчанию разбивка по заводам + выбор политики риска.
/// </summary>
public sealed partial class AnalyticsPanelViewModel : ObservableObject
{
    private readonly string connectionString;
    private UiSettings settings;

    public AnalyticsPanelViewModel(string connectionString)
    {
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

        settings = UiSettingsStore.Load();

        PolicyNames = new ObservableCollection<string>();
        PlantGroups = new ObservableCollection<AnalyticsPlantGroupViewModel>();

        SelectedPolicyForAll = settings.LastPolicyForAll ?? "default";
        statusMessage = "Нажмите «Обновить» для загрузки.";

        BuildDefaultGroups();
    }

    public ObservableCollection<string> PolicyNames { get; }

    public ObservableCollection<AnalyticsPlantGroupViewModel> PlantGroups { get; }

    [ObservableProperty] private string statusMessage;
    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private string selectedPolicyForAll;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    public async Task ApplyPolicyToAllAsync()
    {
        if (IsBusy) return;
        var policy = NormalizePolicyName(SelectedPolicyForAll);
        if (policy is null)
        {
            StatusMessage = "Выберите политику.";
            return;
        }

        foreach (var group in PlantGroups)
        {
            group.SelectedPolicyName = policy;
        }

        SaveSettingsFromGroups(policyForAll: policy);
        await LoadGroupsAsync();
    }

    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Загружаем политики и аналитику...";
            await LoadPolicyNamesAsync();
            NormalizeSelectedPolicies();
            await LoadGroupsAsync();
            StatusMessage = "Готово.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки аналитики: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BuildDefaultGroups()
    {
        PlantGroups.Clear();

        PlantGroups.Add(new AnalyticsPlantGroupViewModel(
            plantCode: "CENTRAL",
            title: "Central",
            plantAliases: new[] { "CENTRAL" },
            includeNullPlantCode: true,
            policyNames: PolicyNames,
            refreshAction: RefreshGroupAsync,
            applyPolicyAction: ApplyGroupPolicyAsync));

        PlantGroups.Add(new AnalyticsPlantGroupViewModel(
            plantCode: "ANPZ",
            title: "ANPZ",
            plantAliases: new[] { "ANPZ" },
            includeNullPlantCode: false,
            policyNames: PolicyNames,
            refreshAction: RefreshGroupAsync,
            applyPolicyAction: ApplyGroupPolicyAsync));

        PlantGroups.Add(new AnalyticsPlantGroupViewModel(
            plantCode: "KNPZ",
            title: "KNPZ",
            plantAliases: new[] { "KNPZ", "KRNPZ" },
            includeNullPlantCode: false,
            policyNames: PolicyNames,
            refreshAction: RefreshGroupAsync,
            applyPolicyAction: ApplyGroupPolicyAsync));

        foreach (var group in PlantGroups)
        {
            group.SelectedPolicyName = GetSavedPolicyOrDefault(group.PlantCode);
        }
    }

    private string GetSavedPolicyOrDefault(string plantCode)
    {
        if (settings.PlantRiskPolicies.TryGetValue(plantCode, out var saved) && !string.IsNullOrWhiteSpace(saved))
        {
            return saved.Trim();
        }

        return "default";
    }

    private async Task ApplyGroupPolicyAsync(AnalyticsPlantGroupViewModel group)
    {
        if (IsBusy || group.IsBusy) return;

        var policy = NormalizePolicyName(group.SelectedPolicyName);
        if (policy is null)
        {
            group.StatusMessage = "Выберите политику.";
            return;
        }

        group.SelectedPolicyName = policy;
        SaveSettingsFromGroups(policyForAll: null);
        await RefreshGroupAsync(group);
    }

    private void SaveSettingsFromGroups(string? policyForAll)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in PlantGroups)
        {
            var policy = NormalizePolicyName(group.SelectedPolicyName);
            if (policy is null) continue;
            map[group.PlantCode] = policy;
        }

        var all = policyForAll ?? settings.LastPolicyForAll;
        settings = new UiSettings(map, all);
        UiSettingsStore.Save(settings);
    }

    private void NormalizeSelectedPolicies()
    {
        var policySet = new HashSet<string>(PolicyNames, StringComparer.OrdinalIgnoreCase);
        if (policySet.Count == 0)
        {
            PolicyNames.Add("default");
            policySet.Add("default");
        }

        var all = NormalizePolicyName(SelectedPolicyForAll) ?? "default";
        if (!policySet.Contains(all)) all = policySet.First();
        SelectedPolicyForAll = all;

        foreach (var group in PlantGroups)
        {
            var selected = NormalizePolicyName(group.SelectedPolicyName) ?? "default";
            if (!policySet.Contains(selected)) selected = all;
            group.SelectedPolicyName = selected;
        }
    }

    private static string? NormalizePolicyName(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private async Task LoadPolicyNamesAsync()
    {
        PolicyNames.Clear();

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            select name
            from public.risk_policies
            order by name
            limit 500
            """;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(name))
            {
                PolicyNames.Add(name.Trim());
            }
        }

        if (PolicyNames.Count == 0)
        {
            PolicyNames.Add("default");
        }
    }

    private async Task LoadGroupsAsync()
    {
        foreach (var group in PlantGroups)
        {
            await RefreshGroupAsync(group);
        }
    }

    private async Task RefreshGroupAsync(AnalyticsPlantGroupViewModel group)
    {
        if (group.IsBusy) return;

        try
        {
            group.IsBusy = true;
            group.StatusMessage = "Загрузка...";
            group.Items.Clear();

            var policy = NormalizePolicyName(group.SelectedPolicyName) ?? "default";
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                with pol as (
                  select threshold_low, threshold_med, threshold_high
                  from public.risk_policies
                  where lower(name) = lower(@policy_name)
                  limit 1
                )
                select
                  ag.asset_code,
                  ag.plant_code,
                  ac.cr,
                  ac.updated_at,
                  case
                    when ac.cr is null then 'UNKNOWN'
                    when pol.threshold_low is null and pol.threshold_med is null and pol.threshold_high is null then '—'
                    when pol.threshold_high is not null and ac.cr >= pol.threshold_high then 'HIGH'
                    when pol.threshold_med is not null and ac.cr >= pol.threshold_med then 'MEDIUM'
                    when pol.threshold_low is not null and ac.cr >= pol.threshold_low then 'LOW'
                    else 'OK'
                  end as risk
                from public.assets_global ag
                left join public.analytics_cr ac on ac.asset_code = ag.asset_code
                left join pol on true
                where (
                  (@include_null and ag.plant_code is null)
                  or lower(coalesce(ag.plant_code, '')) = any(@plants)
                )
                order by ac.cr desc nulls last, ac.updated_at desc nulls last, ag.asset_code
                limit 300
                """;
            cmd.Parameters.AddWithValue("policy_name", policy);
            cmd.Parameters.AddWithValue("include_null", group.IncludeNullPlantCode);

            var plants = group.PlantAliases
                .Select(p => p.Trim().ToLowerInvariant())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var plantParam = cmd.Parameters.Add("plants", NpgsqlDbType.Array | NpgsqlDbType.Text);
            plantParam.Value = plants;

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var asset = reader.IsDBNull(0) ? "—" : reader.GetString(0);
                var plant = reader.IsDBNull(1) ? "—" : reader.GetString(1);
                var cr = reader.IsDBNull(2) ? (decimal?)null : reader.GetFieldValue<decimal>(2);

                DateTimeOffset? updatedAt = null;
                if (!reader.IsDBNull(3))
                {
                    var dt = reader.GetFieldValue<DateTime>(3);
                    if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                    updatedAt = new DateTimeOffset(dt).ToUniversalTime();
                }

                var risk = reader.IsDBNull(4) ? "—" : reader.GetString(4);

                group.Items.Add(new AnalyticsRowViewModel(
                    asset,
                    FormatPlant(plant),
                    cr is null ? "—" : cr.Value.ToString("0.0000", CultureInfo.InvariantCulture),
                    risk,
                    updatedAt?.ToString("u", CultureInfo.InvariantCulture) ?? "—"));
            }

            group.SummaryText = BuildSummary(group.Items);
            group.StatusMessage = $"Строк: {group.Items.Count} · политика: {policy}";
        }
        catch (Exception ex)
        {
            group.StatusMessage = $"Ошибка: {ex.Message}";
        }
        finally
        {
            group.IsBusy = false;
        }
    }

    private static string BuildSummary(IReadOnlyCollection<AnalyticsRowViewModel> rows)
    {
        if (rows.Count == 0) return "Пусто";

        var byRisk = rows
            .GroupBy(r => r.Risk ?? "—", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        int Get(string key) => byRisk.TryGetValue(key, out var v) ? v : 0;

        return $"Всего: {rows.Count} · OK: {Get("OK")} · LOW: {Get("LOW")} · MEDIUM: {Get("MEDIUM")} · HIGH: {Get("HIGH")} · UNKNOWN: {Get("UNKNOWN")}";
    }

    private static string FormatPlant(string plant)
    {
        if (string.IsNullOrWhiteSpace(plant)) return "—";

        var upper = plant.Trim().ToUpperInvariant();
        if (upper == "KRNPZ") return "KNPZ";
        return upper;
    }
}

public sealed record AnalyticsRowViewModel(string AssetCode, string Plant, string CrDisplay, string Risk, string UpdatedAt);
