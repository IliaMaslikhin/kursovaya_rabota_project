using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OilErp.Ui.ViewModels;

public sealed partial class McpSectionViewModel : ObservableObject
{
    public McpSectionViewModel(
        string key,
        string title,
        string summary,
        string tagline,
        string glyphDark,
        string glyphLight,
        IReadOnlyList<string> priorityItems)
    {
        Key = key;
        Title = title;
        Summary = summary;
        Tagline = tagline;
        GlyphDark = glyphDark;
        GlyphLight = glyphLight;
        glyph = glyphDark;
        PriorityItems = priorityItems;
    }

    public string Key { get; }

    public string Title { get; }

    public string Summary { get; }

    public string Tagline { get; }

    public string GlyphDark { get; }

    public string GlyphLight { get; }

    public IReadOnlyList<string> PriorityItems { get; }

    [ObservableProperty]
    private string glyph;

    public void ApplyTheme(bool isDark)
    {
        Glyph = isDark ? GlyphDark : GlyphLight;
    }
}
