using System.Collections.Concurrent;
using MediatR;
using Microsoft.Extensions.Options;
using NMS.Application.Abstractions;

namespace NMS.Application.Common;

public sealed class CachingOptions
{
    public const string Section = "Caching";

    /// <summary>How many seconds a manager overview screen may be a little out of date. 0 turns caching off.</summary>
    public int ManagerScreensSeconds { get; set; } = 10;
}

/// <summary>A read that may be answered from a few seconds ago. The key says exactly which answer it is (dates, layers and so on).</summary>
public interface IShortCachedQuery
{
    string CacheKey { get; }
}

/// <summary>
/// Remembers answers for a few seconds. Kept per centre and per person, so nobody ever sees another person's or centre's answer.
/// When many identical questions arrive together, only the first goes to the database and the rest wait for its answer.
/// </summary>
public sealed class ShortCache
{
    private readonly ConcurrentDictionary<string, (DateTimeOffset Expires, Lazy<Task<object>> Answer)> items = new();

    public async Task<object> GetOrAddAsync(string key, DateTimeOffset now, TimeSpan lifetime, Func<Task<object>> work, Func<object, bool> keep)
    {
        if (items.Count > 5000)
        {
            foreach (var stale in items.Where(i => i.Value.Expires <= now).Select(i => i.Key).ToList())
            {
                items.TryRemove(stale, out _);
            }
        }

        var entry = items.AddOrUpdate(
            key,
            _ => (now + lifetime, new Lazy<Task<object>>(work)),
            (_, old) => old.Expires > now ? old : (now + lifetime, new Lazy<Task<object>>(work)));
        try
        {
            var answer = await entry.Answer.Value;
            if (!keep(answer))
            {
                items.TryRemove(new KeyValuePair<string, (DateTimeOffset, Lazy<Task<object>>)>(key, entry));
            }

            return answer;
        }
        catch
        {
            items.TryRemove(new KeyValuePair<string, (DateTimeOffset, Lazy<Task<object>>)>(key, entry));
            throw;
        }
    }
}

/// <summary>Answers the busiest manager overview reads from a ten-second memory so a crowd of managers opening the same screen costs one database visit.</summary>
public sealed class ShortCacheBehavior<TRequest, TResponse>(ShortCache cache, IOptions<CachingOptions> options, ITenantContext tenant, ICurrentUser user, IClock clock)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var seconds = options.Value.ManagerScreensSeconds;
        if (seconds <= 0 || request is not IShortCachedQuery cached)
        {
            return await next();
        }

        var key = $"{typeof(TRequest).Name}|{tenant.TenantId}|{user.UserId}|{cached.CacheKey}";
        var answer = await cache.GetOrAddAsync(key, clock.UtcNow, TimeSpan.FromSeconds(seconds), async () => (await next())!, r => r is NMS.Shared.Results.Result { IsSuccess: true });
        return (TResponse)answer;
    }
}
