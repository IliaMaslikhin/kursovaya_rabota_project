namespace OilErp.Core.Dto;

/// <summary>
/// Command specification for database operations
/// </summary>
/// <param name="OperationName">Name of the operation to execute</param>
/// <param name="Parameters">Parameters for the operation</param>
/// <param name="TimeoutSeconds">Optional timeout in seconds</param>
public record CommandSpec(
    string OperationName,
    IReadOnlyDictionary<string, object?> Parameters,
    int? TimeoutSeconds = null
);
