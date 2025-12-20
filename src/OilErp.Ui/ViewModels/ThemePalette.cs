using Avalonia.Media;

namespace OilErp.Ui.ViewModels;

public sealed class ThemePalette
{
    public ThemePalette(
        IBrush windowBackground,
        IBrush panelBackground,
        IBrush cardBackground,
        IBrush borderBrush,
        IBrush primaryTextBrush,
        IBrush secondaryTextBrush,
        IBrush accentBrush,
        IBrush accentTextBrush,
        IBrush accentMutedBrush,
        IBrush criticalBrush)
    {
        WindowBackground = windowBackground;
        PanelBackground = panelBackground;
        CardBackground = cardBackground;
        BorderBrush = borderBrush;
        PrimaryTextBrush = primaryTextBrush;
        SecondaryTextBrush = secondaryTextBrush;
        AccentBrush = accentBrush;
        AccentTextBrush = accentTextBrush;
        AccentMutedBrush = accentMutedBrush;
        CriticalBrush = criticalBrush;
    }

    public IBrush WindowBackground { get; }

    public IBrush PanelBackground { get; }

    public IBrush CardBackground { get; }

    public IBrush BorderBrush { get; }

    public IBrush PrimaryTextBrush { get; }

    public IBrush SecondaryTextBrush { get; }

    public IBrush AccentBrush { get; }

    public IBrush AccentTextBrush { get; }

    public IBrush AccentMutedBrush { get; }

    public IBrush CriticalBrush { get; }

    public static ThemePalette UltraBlack { get; } = new ThemePalette(
        new SolidColorBrush(Color.Parse("#000000")),
        new SolidColorBrush(Color.Parse("#080A0F")),
        new SolidColorBrush(Color.Parse("#0D1018")),
        new SolidColorBrush(Color.Parse("#1C1F29")),
        new SolidColorBrush(Color.Parse("#E9ECF5")),
        new SolidColorBrush(Color.Parse("#8A8EA0")),
        new SolidColorBrush(Color.Parse("#FFFFFF")),
        new SolidColorBrush(Color.Parse("#000000")),
        new SolidColorBrush(Color.Parse("#22FFFFFF")),
        new SolidColorBrush(Color.Parse("#F87171")));

    public static ThemePalette JetBrainsLight { get; } = new ThemePalette(
        new SolidColorBrush(Color.Parse("#F3F4F6")),
        new SolidColorBrush(Color.Parse("#FFFFFF")),
        new SolidColorBrush(Color.Parse("#FFFFFF")),
        new SolidColorBrush(Color.Parse("#D7D9E0")),
        new SolidColorBrush(Color.Parse("#1F2022")),
        new SolidColorBrush(Color.Parse("#5C6066")),
        new SolidColorBrush(Color.Parse("#0E7AFF")),
        new SolidColorBrush(Color.Parse("#FFFFFF")),
        new SolidColorBrush(Color.Parse("#D6E8FF")),
        new SolidColorBrush(Color.Parse("#D92B2B")));

    // Алиасы для совместимости со старыми вызовами.
    public static ThemePalette Dark => UltraBlack;

    public static ThemePalette Light => JetBrainsLight;
}
