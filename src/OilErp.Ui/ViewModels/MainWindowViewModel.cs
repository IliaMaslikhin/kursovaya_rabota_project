using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using OilErp.Core.Dto;
using OilErp.Ui.Models;
using OilErp.Ui.Services;

namespace OilErp.Ui.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly KernelGateway kernelGateway;
    private readonly List<MeasurementSeries> measurementSeries;
    private readonly ObservableCollection<MeasurementAnalyticsEntryViewModel> analyticsEntriesInternal;
    private readonly ObservableCollection<DiagnosticEntryViewModel> diagnosticsInternal;

    public MainWindowViewModel()
    {
        kernelGateway = KernelGateway.Create();
        var dataProvider = new MeasurementDataProvider(kernelGateway.Storage, MeasurementSnapshotService.CreateDefault());
        var dataResult = dataProvider.Load();
        measurementSeries = dataResult.Series.ToList();

        analyticsEntriesInternal = new ObservableCollection<MeasurementAnalyticsEntryViewModel>(
            measurementSeries.SelectMany(ToAnalyticsEntries).OrderByDescending(e => e.Measurement.Ts));
        AnalyticsEntries = new ReadOnlyObservableCollection<MeasurementAnalyticsEntryViewModel>(analyticsEntriesInternal);

        diagnosticsInternal = new ObservableCollection<DiagnosticEntryViewModel>(BuildDiagnostics());
        Diagnostics = new ReadOnlyObservableCollection<DiagnosticEntryViewModel>(diagnosticsInternal);

        AddMeasurementForm = new AddMeasurementFormViewModel(measurementSeries, HandleMeasurementAdded);
        AddOperationForm = new AddOperationFormViewModel(kernelGateway.Storage);

        MissionStatement = "Единое окно для диспетчеризации производства, запасов и логистики.";
        EnvironmentLabel = BuildEnvironmentLabel();
        LastSyncDisplay = BuildSyncLabel();
        DataSourceStatus = dataResult.StatusMessage;
        KernelStatus = kernelGateway.StatusMessage;

        palette = ThemePalette.Dark;
        currentTheme = ThemeVariant.Dark;
        isDarkTheme = true;

        Sections = BuildSections();
        AnalyticsSummary = BuildAnalyticsSummary();
        StatusPulses = BuildStatusPulses();
        ApplyThemeToSections();

        SelectedSection = Sections.FirstOrDefault();
    }

    public string ShellTitle => "Консоль управления OilErp";

    public string EnvironmentLabel { get; }

    public string MissionStatement { get; }

    public string KernelStatus { get; }

    public string DataSourceStatus { get; }

    [ObservableProperty]
    private string lastSyncDisplay;

    [ObservableProperty]
    private IReadOnlyList<McpSectionViewModel> sections = Array.Empty<McpSectionViewModel>();

    [ObservableProperty]
    private IReadOnlyList<StatusPulseViewModel> statusPulses = Array.Empty<StatusPulseViewModel>();

    public ReadOnlyObservableCollection<DiagnosticEntryViewModel> Diagnostics { get; }

    public ReadOnlyObservableCollection<MeasurementAnalyticsEntryViewModel> AnalyticsEntries { get; }

    public AddMeasurementFormViewModel AddMeasurementForm { get; }

    public AddOperationFormViewModel AddOperationForm { get; }

    [ObservableProperty]
    private string analyticsSummary = string.Empty;

    [ObservableProperty]
    private ThemePalette palette;

    [ObservableProperty]
    private ThemeVariant currentTheme;

    [ObservableProperty]
    private bool isDarkTheme;

    [ObservableProperty]
    private McpSectionViewModel? selectedSection;

    partial void OnIsDarkThemeChanged(bool value)
    {
        Palette = value ? ThemePalette.Dark : ThemePalette.Light;
        CurrentTheme = value ? ThemeVariant.Dark : ThemeVariant.Light;
        ApplyThemeToSections();
        StatusPulses = BuildStatusPulses();
    }

    private void HandleMeasurementAdded(AddMeasurementRequest request)
    {
        var series = measurementSeries.FirstOrDefault(s => s.SourcePlant == request.Plant && s.AssetCode == request.AssetCode);
        var isNewSeries = false;
        if (series is null)
        {
            series = new MeasurementSeries(request.AssetCode, request.Plant, Array.Empty<MeasurementPointDto>());
            measurementSeries.Add(series);
            isNewSeries = true;
        }

        series.AddPoint(request.Measurement);
        if (isNewSeries)
        {
            AddMeasurementForm.RegisterSeries(series);
        }
        analyticsEntriesInternal.Insert(0, new MeasurementAnalyticsEntryViewModel(request.Plant, request.AssetCode, request.Measurement));
        diagnosticsInternal.Insert(0,
            new DiagnosticEntryViewModel(
                DateTime.Now.ToString("HH:mm"),
                $"Добавлен замер {request.Measurement.Label}",
                $"{request.Plant} · {request.AssetCode} = {request.Measurement.Thickness:F1} мм"));

        AnalyticsSummary = BuildAnalyticsSummary();
        LastSyncDisplay = BuildSyncLabel();
        StatusPulses = BuildStatusPulses();
        RefreshSections();
    }

    private void RefreshSections()
    {
        var previousKey = SelectedSection?.Key;
        Sections = BuildSections();
        ApplyThemeToSections();
        SelectedSection = Sections.FirstOrDefault(s => s.Key == previousKey) ?? Sections.FirstOrDefault();
    }

    private void ApplyThemeToSections()
    {
        foreach (var section in Sections)
        {
            section.ApplyTheme(IsDarkTheme);
        }
    }

    private IReadOnlyList<McpSectionViewModel> BuildSections()
    {
        var overviewItems = measurementSeries
            .Select(FormatLatestSummary)
            .ToList();

        var plantsItems = measurementSeries
            .GroupBy(s => s.SourcePlant)
            .Select(g => $"{g.Key}: {g.Count()} активов, {g.Sum(x => x.Points.Count)} замеров")
            .ToList();

        var productionItems = measurementSeries
            .Select(s => $"{s.AssetCode}: Δ{FormatDelta(s.Trend)} мм · актуальная метка {s.LatestPoint?.Label ?? "—"}")
            .ToList();

        var storageItems = measurementSeries
            .SelectMany(s => s.Points
                .OrderByDescending(p => p.Ts)
                .Take(1)
                .Select(p => $"{s.SourcePlant} · {s.AssetCode} — {p.Thickness:F1} мм ({p.Note ?? "без прим."})"))
            .ToList();

        var logisticsItems = measurementSeries
            .Select(s => $"{s.SourcePlant} ↔ {s.AssetCode}: {s.Points.Count} точек контроля")
            .ToList();

        var analyticsItems = analyticsEntriesInternal
            .Take(4)
            .Select(e => $"{e.Plant} · {e.Label}: {e.ThicknessDisplay} ({e.TimestampDisplay})")
            .ToList();

        var settingsItems = new List<string>
        {
            IsDarkTheme ? "Тёмная тема активна" : "Светлая тема активна",
            $"Логов: {diagnosticsInternal.Count}",
            $"Данных: {measurementSeries.Sum(s => s.Points.Count)} замеров"
        };

        return new List<McpSectionViewModel>
        {
            new McpSectionViewModel(
                "overview",
                "Обзор",
                "Мгновенная сводка по толщинометрии.",
                "Контроль изменения стенок по всем активам.",
                "◎",
                "◉",
                overviewItems),
            new McpSectionViewModel(
                "plants",
                "Заводы",
                "Контекст по каждой площадке.",
                "Сравнение активов и плотности наблюдений.",
                "⚙",
                "⚡",
                plantsItems),
            new McpSectionViewModel(
                "production",
                "Производство",
                "Хронология партий и операций.",
                "Тренды толщины по технологическим ниткам.",
                "▼",
                "⬇",
                productionItems),
            new McpSectionViewModel(
                "storage",
                "Склады",
                "Инвентаризация резервуаров и хранилищ.",
                "Актуальные замеры и пометки инспекторов.",
                "▣",
                "⬒",
                storageItems),
            new McpSectionViewModel(
                "logistics",
                "Логистика",
                "Управление отгрузками и перемещениями.",
                "Связь между заводами и активами контроля.",
                "⇄",
                "⇶",
                logisticsItems),
            new McpSectionViewModel(
                "analytics",
                "Аналитика",
                "Рабочее место для сценариев и KPI.",
                "Фактические DTO из OilErp.Core для построения графиков.",
                "⌗",
                "▤",
                analyticsItems),
            new McpSectionViewModel(
                "settings",
                "Настройки",
                "Тема и окружение.",
                "Параметры отображения и локальные ресурсы.",
                "☰",
                "≡",
                settingsItems),
        };
    }

    private string FormatLatestSummary(MeasurementSeries series)
    {
        if (series.LatestPoint is null)
        {
            return $"{series.SourcePlant} · {series.AssetCode}: нет данных";
        }

        var latest = series.LatestPoint;
        return $"{series.SourcePlant} · {latest.Label}: {latest.Thickness:F1} мм ({FormatDelta(series.Trend)} мм)";
    }

    private static string FormatDelta(decimal value)
    {
        var sign = value > 0 ? "+" : value < 0 ? "-" : "±";
        return $"{sign}{Math.Abs(value):0.0}";
    }

    private IEnumerable<MeasurementAnalyticsEntryViewModel> ToAnalyticsEntries(MeasurementSeries series)
        => series.Points.Select(p => new MeasurementAnalyticsEntryViewModel(series.SourcePlant, series.AssetCode, p));

    private string BuildAnalyticsSummary()
    {
        var plants = measurementSeries.Select(s => s.SourcePlant).Distinct().Count();
        var assets = measurementSeries.Count;
        var measurements = measurementSeries.Sum(s => s.Points.Count);
        var latest = LatestMeasurement();
        var latestText = latest is null ? "нет данных" : latest.Ts.ToString("dd MMM HH:mm");
        return $"Загружено {measurements} замеров по {assets} активам ({plants} завода). Последний: {latestText} UTC.";
    }

    private string BuildEnvironmentLabel()
    {
        var plants = measurementSeries
            .Select(s => s.SourcePlant)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (plants.Count == 0)
        {
            return kernelGateway.IsLive ? "Подключение к ядру" : "Офлайн режим";
        }

        return string.Join(" · ", plants);
    }

    private string BuildSyncLabel()
    {
        var latest = LatestMeasurement();
        return latest is null
            ? "Синхронизация: нет данных"
            : $"Синхронизация {latest.Ts:HH:mm} UTC";
    }

    private IReadOnlyList<StatusPulseViewModel> BuildStatusPulses()
    {
        var measurements = measurementSeries.Sum(s => s.Points.Count);
        var latest = LatestMeasurement();
        var latestDisplay = latest is null ? "нет данных" : latest.Ts.ToString("dd MMM HH:mm");
        var alert = measurementSeries.Any(s => s.Trend <= -0.5m);

        return new[]
        {
            new StatusPulseViewModel("Синхронизация", "OK", $"Последний замер: {latestDisplay} UTC"),
            new StatusPulseViewModel("Измерения", measurements.ToString(), "Всего записей толщинометрии"),
            new StatusPulseViewModel("Аналитика", $"{measurementSeries.Count} активов", "DTO доступны для построения графиков", alert),
            new StatusPulseViewModel("Тема", IsDarkTheme ? "Тёмная" : "Светлая", "Переключите для оператора"),
        };
    }

    private IEnumerable<DiagnosticEntryViewModel> BuildDiagnostics()
    {
        var latestPoints = measurementSeries
            .SelectMany(s => s.Points.Select(p => (Series: s, Point: p)))
            .OrderByDescending(x => x.Point.Ts)
            .Take(3)
            .Select(x => new DiagnosticEntryViewModel(
                x.Point.Ts.ToLocalTime().ToString("HH:mm"),
                $"Получен замер {x.Point.Label}",
                $"{x.Series.SourcePlant} · {x.Series.AssetCode} = {x.Point.Thickness:F1} мм ({x.Point.Note ?? "без прим."})"))
            .ToList();

        if (latestPoints.Count == 0)
        {
            latestPoints.Add(new DiagnosticEntryViewModel("—", "Нет диагностических событий", "Загрузите измерения чтобы увидеть журнал."));
        }

        return latestPoints;
    }

    private MeasurementPointDto? LatestMeasurement()
        => measurementSeries
            .SelectMany(s => s.Points)
            .OrderByDescending(p => p.Ts)
            .FirstOrDefault();
}
