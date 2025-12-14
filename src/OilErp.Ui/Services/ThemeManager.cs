using Avalonia;
using Avalonia.Styling;
using OilErp.Ui.ViewModels;

namespace OilErp.Ui.Services;

public static class ThemeManager
{
    public static void Apply(ThemePalette palette, ThemeVariant variant)
    {
        if (Application.Current is not Application app)
        {
            return;
        }

        app.Resources["Brush.WindowBackground"] = palette.WindowBackground;
        app.Resources["Brush.PanelBackground"] = palette.PanelBackground;
        app.Resources["Brush.CardBackground"] = palette.CardBackground;
        app.Resources["Brush.Border"] = palette.BorderBrush;
        app.Resources["Brush.TextPrimary"] = palette.PrimaryTextBrush;
        app.Resources["Brush.TextSecondary"] = palette.SecondaryTextBrush;
        app.Resources["Brush.Accent"] = palette.AccentBrush;
        app.Resources["Brush.AccentMuted"] = palette.AccentMutedBrush;
        app.Resources["Brush.Critical"] = palette.CriticalBrush;

        app.RequestedThemeVariant = variant;
    }
}
