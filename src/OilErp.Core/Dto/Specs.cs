using System;
using System.Collections.Generic;

namespace OilErp.Core.Dto;

public sealed record CommandSpec(
    string OperationName,
    Dictionary<string, object?> Parameters,
    int TimeoutSeconds = 30);

public sealed record QuerySpec(
    string OperationName,
    Dictionary<string, object?> Parameters,
    int TimeoutSeconds = 30);

public sealed record OperationResult<T>(bool Success, string? ErrorMessage, IReadOnlyList<T> Rows)
{
    public static OperationResult<T> Ok(IReadOnlyList<T> rows) => new(true, null, rows);
    public static OperationResult<T> Fail(string message) => new(false, message, Array.Empty<T>());
}
