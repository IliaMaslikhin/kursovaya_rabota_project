using Avalonia.Styling;

namespace OilErp.Ui.ViewModels;

public sealed record ThemeOption(string Code, string Title, ThemePalette Palette, ThemeVariant Variant)
{
    public override string ToString() => Title;
}
