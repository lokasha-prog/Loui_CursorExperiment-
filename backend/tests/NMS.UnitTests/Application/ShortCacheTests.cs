using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Options;
using NMS.Application.Abstractions;
using NMS.Application.Common;
using NMS.Shared.Results;
using Xunit;

namespace NMS.UnitTests.Application;

public class ShortCacheTests
{
    private sealed record Ask(string Key) : IRequest<Result<int>>, IShortCachedQuery
    {
        public string CacheKey => Key;
    }

    private sealed class Tenant : ITenantContext
    {
        public Guid TenantId { get; set; } = Guid.NewGuid();

        public bool IsInitialized => true;
    }

    private sealed class Person(Guid id) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid UserId => id;

        public string? Email => null;

        public IReadOnlyCollection<string> Roles => [];

        public IReadOnlySet<string> Permissions => new HashSet<string>();

        public string? IpAddress => null;

        public string? UserAgent => null;

        public string? RequestId => null;

        public bool IsInRole(string role) => false;

        public bool HasPermission(string permission) => false;
    }

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);
    }

    private static async Task<int> RunAsync(ShortCacheBehavior<Ask, Result<int>> behavior, Ask ask, Func<int> work) =>
        (await behavior.Handle(ask, _ => Task.FromResult(Result<int>.Success(work())), CancellationToken.None)).Value;

    private static ShortCacheBehavior<Ask, Result<int>> Make(ShortCache cache, Tenant tenant, Person person, Clock clock, int seconds = 10) =>
        new(cache, Options.Create(new CachingOptions { ManagerScreensSeconds = seconds }), tenant, person, clock);

    [Fact]
    public async Task The_same_person_asking_again_within_the_window_gets_the_remembered_answer_until_it_expires()
    {
        var cache = new ShortCache();
        var clock = new Clock();
        var behavior = Make(cache, new Tenant(), new Person(Guid.NewGuid()), clock);
        var calls = 0;

        var first = await RunAsync(behavior, new Ask("a"), () => ++calls);
        var second = await RunAsync(behavior, new Ask("a"), () => ++calls);
        clock.UtcNow = clock.UtcNow.AddSeconds(11);
        var third = await RunAsync(behavior, new Ask("a"), () => ++calls);

        (first, second, third).Should().Be((1, 1, 2));
    }

    [Fact]
    public async Task Another_person_another_centre_or_another_question_never_shares_an_answer()
    {
        var cache = new ShortCache();
        var clock = new Clock();
        var tenant = new Tenant();
        var calls = 0;

        var a = await RunAsync(Make(cache, tenant, new Person(Guid.NewGuid()), clock), new Ask("a"), () => ++calls);
        var otherPerson = await RunAsync(Make(cache, tenant, new Person(Guid.NewGuid()), clock), new Ask("a"), () => ++calls);
        var otherCentre = await RunAsync(Make(cache, new Tenant(), new Person(Guid.NewGuid()), clock), new Ask("a"), () => ++calls);
        var otherQuestion = await RunAsync(Make(cache, tenant, new Person(Guid.NewGuid()), clock), new Ask("b"), () => ++calls);

        new[] { a, otherPerson, otherCentre, otherQuestion }.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task Zero_seconds_turns_it_off_and_failures_are_never_remembered()
    {
        var person = new Person(Guid.NewGuid());
        var off = Make(new ShortCache(), new Tenant(), person, new Clock(), seconds: 0);
        var calls = 0;
        await RunAsync(off, new Ask("a"), () => ++calls);
        await RunAsync(off, new Ask("a"), () => ++calls);

        var on = Make(new ShortCache(), new Tenant(), person, new Clock());
        var failing = 0;
        Task<Result<int>> Fail(CancellationToken _) { failing++; return Task.FromResult(Result<int>.Failure(Error.Validation("no"))); }
        await on.Handle(new Ask("a"), Fail, CancellationToken.None);
        await on.Handle(new Ask("a"), Fail, CancellationToken.None);

        (calls, failing).Should().Be((2, 2));
    }
}
