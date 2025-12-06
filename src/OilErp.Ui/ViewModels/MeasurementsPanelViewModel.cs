using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OilErp.Bootstrap;
using OilErp.Ui.Services;
using OilErp.Ui.Models;
using OilErp.Core.Dto;

namespace OilErp.Ui.ViewModels;

public sealed partial class MeasurementsPanelViewModel : ObservableObject
{
    private readonly MeasurementDataProvider dataProvider;
    private readonly MeasurementSnapshotService snapshotService;
    private readonly MeasurementIngestionService ingestionService;

    public MeasurementsPanelViewModel(MeasurementDataProvider dataProvider, MeasurementSnapshotService snapshotService, MeasurementIngestionService ingestionService)
    {
        this.dataProvider = dataProvider;
        this.snapshotService = snapshotService;
        this.ingestionService = ingestionService;
        Series = new ObservableCollection<MeasurementSeries>();
        measurementForm = new AddMeasurementFormViewModel(Series, SubmitAsync);
        statusMessage = "Нажмите «Обновить» для загрузки данных.";
    }

    public ObservableCollection<MeasurementSeries> Series { get; }

    [ObservableProperty] private string statusMessage;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private AddMeasurementFormViewModel measurementForm;

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Загрузка измерений...";
            Series.Clear();
            AppLogger.Info("[ui] measurements load start");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var result = await dataProvider.LoadAsync(cts.Token);
            foreach (var s in result.Series)
            {
                Series.Add(s);
            }
            StatusMessage = $"{result.StatusMessage} (рядов: {Series.Count})";
            AppLogger.Info($"[ui] measurements load ok count={Series.Count}");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Таймаут загрузки измерений.";
            AppLogger.Error("[ui] measurements load timeout");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
            AppLogger.Error($"[ui] measurements load error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<MeasurementSubmissionResult> SubmitAsync(AddMeasurementRequest request)
    {
        var result = await ingestionService.IngestAsync(request, default);
        StatusMessage = result.Message;
        if (result.Success)
        {
            // обновить локальный список
            var newPoint = request.Measurement;
            var existing = Series.FirstOrDefault(s => string.Equals(s.AssetCode, request.AssetCode, StringComparison.OrdinalIgnoreCase)
                                                      && string.Equals(s.SourcePlant, request.Plant, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.AddPoint(newPoint);
            }
            else
            {
                Series.Add(new MeasurementSeries(request.AssetCode, request.Plant, new[] { newPoint }));
            }
        }
        return result;
    }
}
