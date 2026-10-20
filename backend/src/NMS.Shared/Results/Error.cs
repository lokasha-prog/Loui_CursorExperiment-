namespace NMS.Shared.Results;

public enum ErrorType
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    InvalidState,
}

public sealed record Error(string Code, string Message, ErrorType Type, string? Field = null)
{
    public static Error Validation(string message, string? field = null) =>
        new("VALIDATION_ERROR", message, ErrorType.Validation, field);

    public static Error NotFound(string message = "Resource not found.") =>
        new("RESOURCE_NOT_FOUND", message, ErrorType.NotFound);

    public static Error Forbidden(string message = "Insufficient permission.") =>
        new("INSUFFICIENT_PERMISSION", message, ErrorType.Forbidden);

    public static Error Unauthorized(string message = "Authentication required.") =>
        new("UNAUTHORIZED", message, ErrorType.Unauthorized);

    public static Error Conflict(string message, string? field = null) =>
        new("DUPLICATE_RESOURCE", message, ErrorType.Conflict, field);

    public static Error InvalidState(string message) =>
        new("INVALID_STATE_TRANSITION", message, ErrorType.InvalidState);
}
