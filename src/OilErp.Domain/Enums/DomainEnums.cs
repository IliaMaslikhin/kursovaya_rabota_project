namespace OilErp.Domain.Enums;

/// <summary>
/// Represents the lifecycle state of an asset
/// </summary>
public enum AssetLifecycleState
{
    Planned,
    Active,
    Maintenance,
    Retired
}

/// <summary>
/// Represents the type of asset in the oil industry
/// </summary>
public enum AssetType
{
    Pipeline,
    Tank,
    Pump,
    Valve,
    Compressor,
    Separator,
    HeatExchanger,
    Instrument,
    Other
}

/// <summary>
/// Represents the severity level of a defect
/// </summary>
public enum DefectSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Represents the status of a work order
/// </summary>
public enum WorkOrderStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled,
    OnHold
}

/// <summary>
/// Represents the priority level of a work order
/// </summary>
public enum WorkOrderPriority
{
    Low,
    Medium,
    High,
    Emergency
}

/// <summary>
/// Represents the type of work being performed
/// </summary>
public enum WorkType
{
    Inspection,
    Preventive,
    Corrective,
    Emergency,
    Predictive,
    Calibration,
    Cleaning,
    Replacement
}

/// <summary>
/// Represents the type of measurement being taken
/// </summary>
public enum MeasurementType
{
    WallThickness,
    Pressure,
    Temperature,
    Flow,
    Vibration,
    Corrosion,
    PitDepth,
    CoatingThickness,
    Hardness,
    Other
}

/// <summary>
/// Represents units of measurement
/// </summary>
public enum MeasurementUnit
{
    Millimeters,
    Inches,
    PSI,
    Bar,
    Celsius,
    Fahrenheit,
    LitersPerMinute,
    GPM,
    Hertz,
    Micrometers,
    Mils
}