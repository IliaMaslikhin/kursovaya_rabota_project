namespace OilErp.Ui.Models;

public sealed record MeasurementSubmissionResult(
    bool Success,
    string Message,
    bool PersistedToDatabase);
