namespace OilErp.Ui.ViewModels;

public sealed class StatusPulseViewModel
{
    public StatusPulseViewModel(string label, string value, string description, bool isCritical = false)
    {
        Label = label;
        Value = value;
        Description = description;
        IsCritical = isCritical;
    }

    public string Label { get; }

    public string Value { get; }

    public string Description { get; }

    public bool IsCritical { get; }
}
