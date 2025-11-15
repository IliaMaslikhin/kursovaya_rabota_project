namespace OilErp.Ui.ViewModels;

public sealed class DiagnosticEntryViewModel
{
    public DiagnosticEntryViewModel(string timestamp, string headline, string detail)
    {
        Timestamp = timestamp;
        Headline = headline;
        Detail = detail;
    }

    public string Timestamp { get; }

    public string Headline { get; }

    public string Detail { get; }
}
