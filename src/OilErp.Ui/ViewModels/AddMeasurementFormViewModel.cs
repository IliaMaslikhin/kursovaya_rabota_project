using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OilErp.Core.Dto;
using OilErp.Ui.Models;

namespace OilErp.Ui.ViewModels;

public sealed partial class AddMeasurementFormViewModel : ObservableObject
{
    private readonly Func<AddMeasurementRequest, Task<MeasurementSubmissionResult>> submitCallback;
    private readonly Dictionary<string, List<string>> plantAssets;

    public AddMeasurementFormViewModel(
        IEnumerable<MeasurementSeries> series,
        Func<AddMeasurementRequest, Task<MeasurementSubmissionResult>> submitCallback)
    {
        this.submitCallback = submitCallback;
        plantAssets = series
            .GroupBy(s => NormalizePlant(s.SourcePlant))
            .ToDictionary(
                g => g.Key,
                g => g.Select(s => s.AssetCode)
                      .Where(a => !string.IsNullOrWhiteSpace(a))
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                      .ToList(),
                StringComparer.OrdinalIgnoreCase);

        if (plantAssets.Count == 0)
        {
            plantAssets["—"] = new List<string>();
        }

        Plants = new ObservableCollection<string>(plantAssets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        Assets = new ObservableCollection<string>();
        measurementDate = DateTime.UtcNow.Date;
        measurementTime = DateTime.UtcNow.TimeOfDay;
        thickness = 12.0;
        statusMessage = "Введите параметры и сохраните замер.";
        SelectedPlant = Plants.FirstOrDefault();
    }

    public ObservableCollection<string> Plants { get; }

    public ObservableCollection<string> Assets { get; }

    [ObservableProperty]
    private string? selectedPlant;

    [ObservableProperty]
    private string? selectedAsset;

    [ObservableProperty]
    private string label = string.Empty;

    [ObservableProperty]
    private DateTime measurementDate;

    [ObservableProperty]
    private TimeSpan measurementTime;

    [ObservableProperty]
    private double thickness;

    [ObservableProperty]
    private string? note;

    [ObservableProperty]
    private string statusMessage;

    partial void OnSelectedPlantChanged(string? value)
    {
        Assets.Clear();
        var key = NormalizePlant(value);
        if (plantAssets.TryGetValue(key, out var list))
        {
            foreach (var asset in list)
            {
                Assets.Add(asset);
            }

            SelectedAsset = Assets.FirstOrDefault();
        }
        else
        {
            SelectedAsset = null;
        }

        NotifySubmitStateChanged();
    }

    partial void OnSelectedAssetChanged(string? value)
    {
        NotifySubmitStateChanged();
        if (value is not null && SelectedPlant is not null)
        {
            StatusMessage = $"Готовы создать замер для {SelectedPlant} · {value}.";
        }
    }

    partial void OnLabelChanged(string value) => NotifySubmitStateChanged();

    private void NotifySubmitStateChanged()
    {
        SubmitCommand.NotifyCanExecuteChanged();
    }

    private bool CanSubmit()
        => !string.IsNullOrWhiteSpace(Label)
           && SelectedPlant is not null
           && SelectedAsset is not null;

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        var timestamp = DateTime.SpecifyKind(MeasurementDate.Date + MeasurementTime, DateTimeKind.Utc);
        var dto = new MeasurementPointDto(
            Label.Trim(),
            timestamp,
            (decimal)Math.Round(Thickness, 2),
            string.IsNullOrWhiteSpace(Note) ? null : Note.Trim());

        var result = await submitCallback(new AddMeasurementRequest(SelectedPlant!, SelectedAsset!, dto));
        StatusMessage = result.Message;

        if (result.Success)
        {
            Label = string.Empty;
            Note = string.Empty;
            Thickness = 12.0;
            MeasurementDate = DateTime.UtcNow.Date;
            MeasurementTime = DateTime.UtcNow.TimeOfDay;
        }
    }

    public void RegisterSeries(MeasurementSeries series)
    {
        var plant = NormalizePlant(series.SourcePlant);
        if (!plantAssets.TryGetValue(plant, out var assets))
        {
            assets = new List<string>();
            plantAssets[plant] = assets;
            Plants.Add(plant);
        }

        if (!assets.Contains(series.AssetCode, StringComparer.OrdinalIgnoreCase))
        {
            assets.Add(series.AssetCode);
            assets.Sort(StringComparer.OrdinalIgnoreCase);
        }

        if (string.Equals(SelectedPlant, plant, StringComparison.OrdinalIgnoreCase)
            && !Assets.Any(a => string.Equals(a, series.AssetCode, StringComparison.OrdinalIgnoreCase)))
        {
            var insertIndex = 0;
            while (insertIndex < Assets.Count
                   && string.Compare(Assets[insertIndex], series.AssetCode, StringComparison.OrdinalIgnoreCase) < 0)
            {
                insertIndex++;
            }
            Assets.Insert(insertIndex, series.AssetCode);
        }
    }

    private static string NormalizePlant(string? plant)
        => string.IsNullOrWhiteSpace(plant) ? "—" : plant.Trim();
}
