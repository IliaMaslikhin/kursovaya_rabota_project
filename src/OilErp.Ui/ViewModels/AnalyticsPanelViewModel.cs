using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npgsql;

namespace OilErp.Ui.ViewModels;

public sealed partial class AnalyticsPanelViewModel : ObservableObject
{
    private readonly string connectionString;

    public AnalyticsPanelViewModel(string connectionString)
    {
        this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
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

    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Загружаем аналитику (все заводы)...";
            Items.Clear();

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                              select
                                src.asset_code,
                                src.plant_code,
                                src.cr,
                                src.updated_at,
                                src.risk
                              from (
                                with pol as (
                                  select threshold_low, threshold_med, threshold_high
                                  from public.risk_policies
                                  where name = 'default'
                                  limit 1
                                ),
                                last_batch as (
                                  select distinct on (asset_code)
                                    asset_code,
                                    source_plant,
                                    prev_thk,
                                    prev_date,
                                    last_thk,
                                    last_date,
                                    created_at
                                  from public.measurement_batches
                                  order by asset_code, last_date desc, id desc
                                ),
                                merged as (
                                  select
                                    coalesce(ag.asset_code, lb.asset_code) as asset_code,
                                    coalesce(ag.plant_code, lb.source_plant) as plant_code,
                                    coalesce(ac.cr, public.fn_calc_cr(lb.prev_thk, lb.prev_date, lb.last_thk, lb.last_date)) as cr,
                                    coalesce(ac.updated_at, lb.created_at, ag.created_at) as updated_at,
                                    pol.threshold_low,
                                    pol.threshold_med,
                                    pol.threshold_high
                                  from public.assets_global ag
                                  full join last_batch lb on lb.asset_code = ag.asset_code
                                  left join public.analytics_cr ac on ac.asset_code = coalesce(ag.asset_code, lb.asset_code)
                                  left join pol on true
                                )
                                select
                                  asset_code,
                                  plant_code,
                                  cr,
                                  updated_at,
                                  case
                                    when cr is null then '—'
                                    when threshold_low is null and threshold_med is null and threshold_high is null then '—'
                                    when threshold_high is not null and cr >= threshold_high then 'HIGH'
                                    when threshold_med is not null and cr >= threshold_med then 'MEDIUM'
                                    when threshold_low is not null and cr >= threshold_low then 'LOW'
                                    else 'OK'
                                  end as risk
                                from merged
                              ) src
                              order by src.cr desc nulls last, src.updated_at desc nulls last, src.asset_code
                              limit 300
                              """;

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var asset = reader.GetString(0);
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

                Items.Add(new AnalyticsRowViewModel(
                    asset,
                    FormatPlant(plant),
                    cr is null ? "—" : cr.Value.ToString("0.0000"),
                    risk ?? "—",
                    updatedAt?.ToString("u") ?? "—"));
            }

            StatusMessage = $"Загружено {Items.Count} строк.";
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

    private static string FormatPlant(string plant)
    {
        if (string.IsNullOrWhiteSpace(plant)) return "—";

        var upper = plant.Trim().ToUpperInvariant();
        if (upper == "KRNPZ") return "KNPZ";
        return upper;
    }
}

public sealed record AnalyticsRowViewModel(string AssetCode, string Plant, string CrDisplay, string Risk, string UpdatedAt);
