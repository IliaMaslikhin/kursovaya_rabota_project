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

    public IBrush AccentMutedBrush { get; }

    public IBrush CriticalBrush { get; }

    public static ThemePalette Dark { get; } = new ThemePalette(
        new SolidColorBrush(Color.Parse("#0F172A")),
        new SolidColorBrush(Color.Parse("#111C34")),
        new SolidColorBrush(Color.Parse("#111C34")),
        new SolidColorBrush(Color.Parse("#1F2A40")),
        new SolidColorBrush(Color.Parse("#F8FAFC")),
        new SolidColorBrush(Color.Parse("#94A3B8")),
        new SolidColorBrush(Color.Parse("#38BDF8")),
        new SolidColorBrush(Color.Parse("#1D4ED8")),
        new SolidColorBrush(Color.Parse("#F87171")));

    public static ThemePalette Light { get; } = new ThemePalette(
        new SolidColorBrush(Color.Parse("#F8FAFC")),
        new SolidColorBrush(Color.Parse("#FFFFFF")),
        new SolidColorBrush(Color.Parse("#FFFFFF")),
        new SolidColorBrush(Color.Parse("#E2E8F0")),
        new SolidColorBrush(Color.Parse("#0F172A")),
        new SolidColorBrush(Color.Parse("#475569")),
        new SolidColorBrush(Color.Parse("#0EA5E9")),
        new SolidColorBrush(Color.Parse("#2563EB")),
        new SolidColorBrush(Color.Parse("#DC2626")));
}
