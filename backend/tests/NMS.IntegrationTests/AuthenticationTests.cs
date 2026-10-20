using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthenticationTests(NmsApiFixture api)
{
    [Fact]
    public async Task Setup_is_already_complete_so_status_says_so_and_bootstrap_is_refused()
    {
        var anonymous = api.NewClient();

        var status = await anonymous.GetAsync("/api/v1/auth/setup-status");
        var again = await anonymous.PostAsync("/api/v1/auth/bootstrap-super-admin", new
        {
            bootstrap_token = api.BootstrapToken,
            tenant_slug = "second",
            tenant_name = new { en = "Second" },
            admin_email = "second@test.local",
            admin_first_name = "S",
            admin_last_name = "S",
            admin_password = NmsApiFixture.NewPassword(),
        });

        status.Data.GetProperty("setup_required").GetBoolean().Should().BeFalse();
        again.Status.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Bootstrap_with_a_wrong_token_looks_like_a_missing_page()
    {
        var reply = await api.NewClient().PostAsync("/api/v1/auth/bootstrap-super-admin", new
        {
            bootstrap_token = "wrong",
            tenant_slug = "x",
            tenant_name = new { en = "X" },
            admin_email = "x@test.local",
            admin_first_name = "X",
            admin_last_name = "X",
            admin_password = NmsApiFixture.NewPassword(),
        });

        reply.Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Protected_endpoints_reject_anonymous_callers()
    {
        var reply = await api.NewClient().GetAsync("/api/v1/students");

        reply.Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Garbage_token_is_rejected()
    {
        var client = api.NewClient();
        client.SetBearer("not.a.jwt");

        (await client.GetAsync("/api/v1/students")).Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_returns_a_token_the_envelope_and_an_httponly_refresh_cookie()
    {
        var reply = await api.NewClient().PostAsync("/api/v1/auth/login", new { tenant = api.TenantSlug, email = api.AdminEmail, password = api.AdminPassword });

        reply.Status.Should().Be(HttpStatusCode.OK);
        reply.Root.GetProperty("success").GetBoolean().Should().BeTrue();
        reply.Data.GetProperty("access_token").GetString().Should().NotBeNullOrEmpty();
        reply.Data.TryGetProperty("refresh_token", out _).Should().BeFalse("the refresh token must only travel in the cookie");
        reply.Meta.GetProperty("request_id").GetString().Should().NotBeNullOrEmpty();
        reply.SetCookieRaw.Should().Contain("httponly").And.Contain("samesite=strict");
    }

    [Fact]
    public async Task Sign_in_must_name_an_existing_centre_when_several_exist()
    {
        var wrong = await api.NewClient().PostAsync("/api/v1/auth/login", new { tenant = "nobody", email = api.AdminEmail, password = api.AdminPassword });
        var right = await api.NewClient().PostAsync("/api/v1/auth/login", new { tenant = api.TenantSlug, email = api.AdminEmail, password = api.AdminPassword });

        wrong.Status.Should().Be(HttpStatusCode.Unauthorized);
        right.Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_give_the_same_answer()
    {
        var wrongPassword = await api.NewClient().PostAsync("/api/v1/auth/login", new { tenant = api.TenantSlug, email = api.AdminEmail, password = "nope" });
        var unknown = await api.NewClient().PostAsync("/api/v1/auth/login", new { tenant = api.TenantSlug, email = "ghost@test.local", password = "nope" });

        wrongPassword.Status.Should().Be(HttpStatusCode.Unauthorized);
        unknown.Status.Should().Be(HttpStatusCode.Unauthorized);
        wrongPassword.Root.GetProperty("errors")[0].GetProperty("message").GetString()
            .Should().Be(unknown.Root.GetProperty("errors")[0].GetProperty("message").GetString());
    }

    [Fact]
    public async Task Me_reports_the_roles_and_permissions_of_the_signed_in_person()
    {
        var teacher = await api.CreateUserAsync("teacher");

        var me = await teacher.Client.GetAsync("/api/v1/auth/me");

        me.Status.Should().Be(HttpStatusCode.OK);
        me.Data.GetProperty("roles")[0].GetString().Should().Be("teacher");
        var permissions = me.Data.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToList();
        permissions.Should().Contain("students.read").And.NotContain("students.write");
    }

    [Fact]
    public async Task Account_locks_after_repeated_failures_even_for_the_right_password()
    {
        var user = await api.CreateUserAsync("receptionist");

        for (var i = 0; i < 5; i++)
        {
            (await api.NewClient().PostAsync("/api/v1/auth/login", new { tenant = api.TenantSlug, email = user.Email, password = "wrong" }))
                .Status.Should().Be(HttpStatusCode.Unauthorized);
        }

        var locked = await api.NewClient().PostAsync("/api/v1/auth/login", new { tenant = api.TenantSlug, email = user.Email, password = user.Password });

        locked.Status.Should().Be(HttpStatusCode.Unauthorized);
        locked.Root.GetProperty("errors")[0].GetProperty("message").GetString().Should().Contain("locked");
    }

    [Fact]
    public async Task Refresh_issues_a_new_token_and_rotates_the_cookie()
    {
        var user = await api.CreateUserAsync("teacher");

        var refreshed = await api.NewClient().RefreshAsync(user.Client.RefreshCookie!);

        refreshed.Status.Should().Be(HttpStatusCode.OK, refreshed.Raw);
        refreshed.Data.GetProperty("access_token").GetString().Should().NotBeNullOrEmpty();
        refreshed.RefreshCookie.Should().NotBeNullOrEmpty().And.NotBe(user.Client.RefreshCookie);
    }

    [Fact]
    public async Task Replaying_a_used_refresh_token_ends_every_session_in_that_family()
    {
        var user = await api.CreateUserAsync("teacher");
        var original = user.Client.RefreshCookie!;
        var rotated = await api.NewClient().RefreshAsync(original);
        var newest = rotated.RefreshCookie!;
        await Task.Delay(TimeSpan.FromSeconds(NmsApiFixture.GraceSeconds + 0.7));

        var replay = await api.NewClient().RefreshAsync(original);
        var afterTheft = await api.NewClient().RefreshAsync(newest);

        replay.Status.Should().Be(HttpStatusCode.Unauthorized);
        afterTheft.Status.Should().Be(HttpStatusCode.Unauthorized, "theft revokes the whole family");
    }

    [Fact]
    public async Task A_quick_duplicate_refresh_such_as_two_tabs_is_tolerated_without_ending_the_session()
    {
        var user = await api.CreateUserAsync("teacher");
        var original = user.Client.RefreshCookie!;

        var first = await api.NewClient().RefreshAsync(original);
        var duplicate = await api.NewClient().RefreshAsync(original);
        var continued = await api.NewClient().RefreshAsync(first.RefreshCookie!);

        first.Status.Should().Be(HttpStatusCode.OK);
        duplicate.Status.Should().Be(HttpStatusCode.OK, "a reload or second tab inside the grace window is not theft");
        continued.Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_requires_the_custom_header_that_blocks_cross_site_requests()
    {
        var user = await api.CreateUserAsync("teacher");

        var reply = await api.NewClient().SendAsync(HttpMethod.Post, "/api/v1/auth/refresh", null, user.Client.RefreshCookie);

        reply.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var user = await api.CreateUserAsync("teacher");

        var logout = await api.NewClient().SendAsync(HttpMethod.Post, "/api/v1/auth/logout", null, user.Client.RefreshCookie, csrfHeader: true);
        var refresh = await api.NewClient().RefreshAsync(user.Client.RefreshCookie!);

        logout.Status.Should().Be(HttpStatusCode.NoContent);
        refresh.Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Suspending_a_user_ends_their_sessions_and_blocks_new_ones()
    {
        var user = await api.CreateUserAsync("teacher");

        var suspend = await api.Admin.PostAsync($"/api/v1/users/{user.UserId}/actions/set-status", new { status = "suspended" });
        var refresh = await api.NewClient().RefreshAsync(user.Client.RefreshCookie!);
        var login = await api.NewClient().PostAsync("/api/v1/auth/login", new { tenant = api.TenantSlug, email = user.Email, password = user.Password });

        suspend.Status.Should().Be(HttpStatusCode.OK);
        refresh.Status.Should().Be(HttpStatusCode.Unauthorized);
        login.Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Only_user_managers_can_create_users_and_duplicates_are_refused()
    {
        var centreAdmin = await api.CreateUserAsync("centre_admin");
        var body = new
        {
            email = $"dup-{Guid.NewGuid():N}@test.local",
            first_name = "A",
            last_name = "B",
            password = NmsApiFixture.NewPassword(),
            roles = new[] { "teacher" },
        };

        var denied = await centreAdmin.Client.PostAsync("/api/v1/users", body);
        var created = await api.Admin.PostAsync("/api/v1/users", body);
        var duplicate = await api.Admin.PostAsync("/api/v1/users", body);

        denied.Status.Should().Be(HttpStatusCode.Forbidden);
        created.Status.Should().Be(HttpStatusCode.Created);
        duplicate.Status.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Weak_passwords_are_refused()
    {
        var reply = await api.Admin.PostAsync("/api/v1/users", new
        {
            email = $"weak-{Guid.NewGuid():N}@test.local",
            first_name = "A",
            last_name = "B",
            password = "short",
            roles = new[] { "teacher" },
        });

        reply.Status.Should().Be(HttpStatusCode.BadRequest);
        reply.ErrorCode.Should().Be("VALIDATION_ERROR");
    }
}
