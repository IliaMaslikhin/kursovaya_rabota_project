namespace OilErp.Core.Dto;

/// <summary>
/// Query specification for database operations
/// </summary>
/// <param name="OperationName">Name of the operation to execute</param>
/// <param name="Parameters">Parameters for the operation</param>
/// <param name="TimeoutSeconds">Optional timeout in seconds</param>
public record QuerySpec(
    string OperationName,
    IReadOnlyDictionary<string, object?> Parameters,
    int? TimeoutSeconds = null
);
