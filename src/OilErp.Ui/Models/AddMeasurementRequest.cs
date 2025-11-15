using OilErp.Core.Dto;

namespace OilErp.Ui.Models;

public readonly record struct AddMeasurementRequest(
    string Plant,
    string AssetCode,
    MeasurementPointDto Measurement);
