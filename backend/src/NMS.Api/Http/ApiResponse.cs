using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Api.Http;

public sealed record ApiError(string Code, string Message, string? Field);

public sealed record ApiPagination(int Limit, bool HasMore, string? NextCursor);

public sealed record ApiMeta(string RequestId, DateTimeOffset Timestamp, ApiPagination? Pagination = null);

/// <summary>The { success, data, meta, errors } envelope every response uses (spec 08 §3).</summary>
public sealed record ApiResponse<T>(bool Success, T? Data, ApiMeta Meta, IReadOnlyList<ApiError>? Errors = null);

public static class ApiResults
{
    public static IResult Ok<T>(HttpContext http, T data, int status = StatusCodes.Status200OK) =>
        Results.Json(new ApiResponse<T>(true, data, Meta(http)), statusCode: status);

    public static IResult Page<T>(HttpContext http, CursorPage<T> page) =>
        Results.Json(new ApiResponse<IReadOnlyList<T>>(
            true, page.Items, Meta(http, new ApiPagination(page.Limit, page.HasMore, page.NextCursor))));

    public static IResult Failure(HttpContext http, IReadOnlyList<Error> errors)
    {
        var status = errors[0].Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.InvalidState => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError,
        };
        var body = new ApiResponse<object>(
            false, null, Meta(http), errors.Select(e => new ApiError(e.Code, e.Message, e.Field)).ToList());
        return Results.Json(body, statusCode: status);
    }

    public static IResult From<T>(HttpContext http, Result<T> result, int successStatus = StatusCodes.Status200OK) =>
        result.IsSuccess ? Ok(http, result.Value, successStatus) : Failure(http, result.Errors);

    public static IResult From(HttpContext http, Result result) =>
        result.IsSuccess ? Results.NoContent() : Failure(http, result.Errors);

    public static IResult FromPage<T>(HttpContext http, Result<CursorPage<T>> result) =>
        result.IsSuccess ? Page(http, result.Value) : Failure(http, result.Errors);

    public static IResult Error(HttpContext http, int status, string code, string message) =>
        Results.Json(
            new ApiResponse<object>(false, null, Meta(http), [new ApiError(code, message, null)]),
            statusCode: status);

    private static ApiMeta Meta(HttpContext http, ApiPagination? pagination = null) =>
        new(http.TraceIdentifier, DateTimeOffset.UtcNow, pagination);
}
