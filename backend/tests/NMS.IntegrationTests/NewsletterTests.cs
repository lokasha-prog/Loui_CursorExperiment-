using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using NMS.Application.Abstractions;
using NMS.Application.Newsletters;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class NewsletterTests(NmsApiFixture api)
{
    private async Task<(ApiClient Client, Guid Child, Guid Room, string Email)> FamilyAsync(Guid? room = null)
    {
        var r = room ?? await api.CreateRoomAsync();
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var child = await api.CreateStudentAsync(r, email);
        await api.Admin.PostAsync($"/api/v1/students/{child}/actions/transition-status", new { status = "active" });
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardian);
        return (parent.Client, child, r, email);
    }

    private async Task<Guid> DraftAsync(string html, string audience = "all", Guid[]? ids = null, string? ar = null)
    {
        var body = new Dictionary<string, string> { ["en"] = html };
        if (ar is not null) body["ar"] = ar;
        var made = await api.Admin.PostAsync("/api/v1/newsletters", new { title = new { en = $"News {Guid.NewGuid():N}"[..14], ar = "أخبار" }, body, audience_type = audience, audience_ids = ids ?? Array.Empty<Guid>() });
        made.Status.Should().Be(HttpStatusCode.Created, made.Raw);
        return made.Data.GetProperty("newsletter").GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Unsafe_html_is_removed_and_only_simple_formatting_and_web_links_stay()
    {
        var id = await DraftAsync("<p onclick=\"steal()\">Hello <script>alert(1)</script><strong>world</strong> <a href=\"javascript:alert(2)\">bad</a> <a href=\"https://example.com/a?x=1&y=2\">good</a><img src=x onerror=alert(3)></p><style>p{}</style>");

        var body = (await api.Admin.GetAsync($"/api/v1/newsletters/{id}")).Data.GetProperty("body").GetProperty("en").GetString()!;

        body.Should().Contain("<strong>world</strong>").And.Contain("https://example.com/a?x=1&amp;y=2");
        body.Should().NotContain("script").And.NotContain("onclick").And.NotContain("javascript:").And.NotContain("<img").And.NotContain("<style");
    }

    [Fact]
    public async Task Publishing_reaches_only_the_chosen_rooms_and_the_parent_sees_it_marks_it_opened_and_clicks_are_counted()
    {
        var room = await api.CreateRoomAsync();
        var inRoom = await FamilyAsync(room);
        var elsewhere = await FamilyAsync();
        var id = await DraftAsync("<p>Open day <a href=\"https://example.com/open-day\">details</a></p>", "room", [room], "<p>يوم مفتوح</p>");

        var published = await api.Admin.PostAsync($"/api/v1/newsletters/{id}/actions/publish");
        var mineIn = await inRoom.Client.GetAsync("/api/v1/newsletters/mine");
        var mineOut = await elsewhere.Client.GetAsync("/api/v1/newsletters/mine");
        var read = await inRoom.Client.GetAsync($"/api/v1/newsletters/{id}/read");
        var readOut = await elsewhere.Client.GetAsync($"/api/v1/newsletters/{id}/read");
        var link = System.Text.RegularExpressions.Regex.Match(read.Data.GetProperty("body").GetString()!, "href=\"([^\"]+)\"").Groups[1].Value;
        var anon = api.NewClient();
        var click = await anon.GetRedirectAsync(link);
        var stats = (await api.Admin.GetAsync($"/api/v1/newsletters/{id}/stats")).Data;

        published.Status.Should().Be(HttpStatusCode.OK, published.Raw);
        published.Data.GetProperty("newsletter").GetProperty("status").GetString().Should().Be("published");
        mineIn.Items.Should().Contain(n => n.GetProperty("id").GetGuid() == id);
        mineOut.Items.Should().NotContain(n => n.GetProperty("id").GetGuid() == id);
        readOut.Status.Should().Be(HttpStatusCode.NotFound);
        link.Should().StartWith("/api/v1/public/newsletters/click/");
        click.Status.Should().Be(HttpStatusCode.Redirect);
        click.Location.Should().Be("https://example.com/open-day");
        stats.GetProperty("recipients").GetInt32().Should().Be(1);
        stats.GetProperty("opened").GetInt32().Should().Be(1);
        stats.GetProperty("clicks").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task A_made_up_click_link_goes_nowhere_and_a_sent_newsletter_cannot_be_edited_or_sent_again()
    {
        var family = await FamilyAsync();
        var id = await DraftAsync("<p>Hello</p>");
        await api.Admin.PostAsync($"/api/v1/newsletters/{id}/actions/publish");
        var anon = api.NewClient();

        var guessed = await anon.GetRedirectAsync($"/api/v1/public/newsletters/click/{api.TenantId:N}.{new string('a', 48)}/0");
        var outOfRange = await anon.GetRedirectAsync($"/api/v1/public/newsletters/click/{api.TenantId:N}.x/9");
        var edit = await api.Admin.PatchAsync($"/api/v1/newsletters/{id}", new { title = new { en = "Changed" }, body = new { en = "<p>x</p>" }, audience_type = "all" });
        var again = await api.Admin.PostAsync($"/api/v1/newsletters/{id}/actions/publish");
        var remove = await api.Admin.DeleteAsync($"/api/v1/newsletters/{id}");
        var archived = await api.Admin.PostAsync($"/api/v1/newsletters/{id}/actions/archive");
        _ = family;

        guessed.Status.Should().Be(HttpStatusCode.NotFound);
        outOfRange.Status.Should().Be(HttpStatusCode.NotFound);
        edit.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        again.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        remove.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        archived.Data.GetProperty("newsletter").GetProperty("status").GetString().Should().Be("archived");
    }

    [Fact]
    public async Task A_scheduled_newsletter_waits_for_its_time_can_be_taken_back_and_is_sent_by_the_job_when_due()
    {
        var family = await FamilyAsync();
        var id = await DraftAsync("<p>Coming soon</p>");
        var past = await api.Admin.PostAsync($"/api/v1/newsletters/{id}/actions/schedule", new { at = DateTimeOffset.UtcNow.AddMinutes(-5) });
        var scheduled = await api.Admin.PostAsync($"/api/v1/newsletters/{id}/actions/schedule", new { at = DateTimeOffset.UtcNow.AddHours(2) });
        var before = await family.Client.GetAsync("/api/v1/newsletters/mine");
        var back = await api.Admin.PostAsync($"/api/v1/newsletters/{id}/actions/unschedule");

        // Make it due (as if the time had come), then let the job run.
        var soon = await api.Admin.PostAsync($"/api/v1/newsletters/{id}/actions/schedule", new { at = DateTimeOffset.UtcNow.AddMinutes(1).AddSeconds(2) });
        using (var scope = api.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = api.TenantId;
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var row = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(db.Newsletters, n => n.Id == id);
            row.ScheduledFor = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync(CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunNewsletterSendsCommand());
        }

        var after = await family.Client.GetAsync("/api/v1/newsletters/mine");

        past.Status.Should().Be(HttpStatusCode.BadRequest);
        scheduled.Data.GetProperty("newsletter").GetProperty("status").GetString().Should().Be("scheduled");
        before.Items.Should().NotContain(n => n.GetProperty("id").GetGuid() == id);
        back.Data.GetProperty("newsletter").GetProperty("status").GetString().Should().Be("draft");
        soon.Status.Should().Be(HttpStatusCode.OK, soon.Raw);
        after.Items.Should().Contain(n => n.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Only_broadcast_roles_manage_newsletters_a_draft_needs_text_and_an_audience_and_the_ai_draft_works_without_a_provider()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var denied = await teacher.Client.GetAsync("/api/v1/newsletters");
        var noAudience = await api.Admin.PostAsync("/api/v1/newsletters", new { title = new { en = "X" }, body = new { en = "<p>x</p>" }, audience_type = "room", audience_ids = Array.Empty<Guid>() });
        var ghostRoom = await api.Admin.PostAsync("/api/v1/newsletters", new { title = new { en = "X" }, body = new { en = "<p>x</p>" }, audience_type = "room", audience_ids = new[] { Guid.NewGuid() } });
        var empty = await DraftAsync("<p></p>");
        var sendEmpty = await api.Admin.PostAsync($"/api/v1/newsletters/{empty}/actions/publish");
        var draft = await api.Admin.PostAsync("/api/v1/newsletters/actions/draft?language=ar");

        denied.Status.Should().Be(HttpStatusCode.Forbidden);
        noAudience.Status.Should().Be(HttpStatusCode.BadRequest);
        ghostRoom.Status.Should().Be(HttpStatusCode.BadRequest);
        sendEmpty.Status.Should().Be(HttpStatusCode.BadRequest);
        draft.Status.Should().Be(HttpStatusCode.OK, draft.Raw);
        draft.Data.GetProperty("body").GetString().Should().Contain("<p>");
    }
}
