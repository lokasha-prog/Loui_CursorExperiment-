using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NMS.Domain.Common;
using NMS.Shared.Paging;
using NMS.Shared.Results;

namespace NMS.Application.Common;

public interface ICursorItem
{
    Guid Id { get; }

    DateTimeOffset CreatedAt { get; }
}

public static class QueryableExtensions
{
    /// <summary>Cursor pagination on (created_at DESC, id DESC) as described in spec 08 §6. Never uses OFFSET.</summary>
    public static async Task<CursorPage<TDto>> ToCursorPageAsync<TEntity, TDto>(
        this IQueryable<TEntity> query,
        string? cursor,
        int? limit,
        System.Linq.Expressions.Expression<Func<TEntity, TDto>> select,
        CancellationToken cancellationToken)
        where TEntity : TenantEntity
        where TDto : ICursorItem
    {
        var pageSize = CursorToken.NormalizeLimit(limit);
        var token = CursorToken.TryDecode(cursor);

        if (token is not null)
        {
            var createdAt = token.CreatedAt;
            var id = token.Id;
            query = query.Where(e => e.CreatedAt < createdAt || (e.CreatedAt == createdAt && e.Id.CompareTo(id) < 0));
        }

        var rows = await query
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Take(pageSize + 1)
            .Select(select)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? new CursorToken(rows[^1].CreatedAt, rows[^1].Id).Encode()
            : null;

        return new CursorPage<TDto>(rows, next, hasMore, pageSize);
    }
}

public static class LocalizedTextValidation
{
    /// <summary>Requires an English value; Arabic is optional.</summary>
    public static IRuleBuilderOptions<T, Dictionary<string, string>> MustHaveEnglish<T>(
        this IRuleBuilder<T, Dictionary<string, string>> rule) =>
        rule.Must(d => d is not null && d.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en))
            .WithMessage("An English value is required.");
}

public static class NameConflict
{
    /// <summary>Case-insensitive English-name clash check. Name sets are small per tenant, so this runs in memory.</summary>
    public static bool Exists(IEnumerable<Dictionary<string, string>> existing, string english) =>
        existing.Any(n => n.TryGetValue("en", out var value)
            && string.Equals(value.Trim(), english.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class BarcodeGenerator
{
    public static string Next(string prefix) =>
        $"{prefix}{Random.Shared.NextInt64(100_000_000, 1_000_000_000)}";
}

/// <summary>Runs FluentValidation validators before any handler and turns failures into a Result.</summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var failures = new List<Error>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken);
            failures.AddRange(result.Errors.Select(f =>
                Error.Validation(f.ErrorMessage, ToSnakeCase(f.PropertyName))));
        }

        if (failures.Count == 0)
        {
            return await next();
        }

        var responseType = typeof(TResponse);
        if (responseType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(failures);
        }

        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var failure = responseType.GetMethod(nameof(Result<object>.Failure), [typeof(IEnumerable<Error>)])!;
            return (TResponse)failure.Invoke(null, [failures])!;
        }

        throw new ValidationException(failures.Select(f => new FluentValidation.Results.ValidationFailure(f.Field, f.Message)));
    }

    private static string? ToSnakeCase(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        return string.Concat(name.Select((c, i) =>
            char.IsUpper(c) && i > 0 && name[i - 1] != '.' ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    }
}
