namespace OilErp.Core.Dto;

/// <summary>
/// Generic operation result wrapper
/// </summary>
/// <typeparam name="T">Type of the operation result data</typeparam>
/// <param name="Data">Operation result data</param>
/// <param name="Errors">Collection of error messages</param>
/// <param name="Meta">Additional metadata</param>
public record OperationResult<T>(
    T? Data,
    IReadOnlyList<string> Errors,
    IReadOnlyDictionary<string, object?>? Meta = null
);
