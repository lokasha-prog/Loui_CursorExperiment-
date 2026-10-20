using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class FeedbackTests(NmsApiFixture api)
{
    [Fact]
    public async Task Anyone_signed_in_can_send_feedback_and_only_managers_can_read_and_triage_it()
    {
        var parent = await api.CreateUserAsync("parent");
        var teacher = await api.CreateUserAsync("teacher");

        var sent = await parent.Client.PostAsync("/api/v1/feedback", new { kind = "problem", message = "The photo did not load", page = "/feed", rating = 2 });
        var listed = await api.Admin.GetAsync("/api/v1/feedback?status=new&limit=100");
        var id = sent.Data.GetProperty("id").GetGuid();
        var done = await api.Admin.PatchAsync($"/api/v1/feedback/{id}", new { status = "done" });

        sent.Status.Should().Be(HttpStatusCode.Created, sent.Raw);
        listed.Items.Should().Contain(f => f.GetProperty("id").GetGuid() == id && f.GetProperty("page").GetString() == "/feed");
        done.Data.GetProperty("status").GetString().Should().Be("done");
        (await parent.Client.GetAsync("/api/v1/feedback")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await teacher.Client.PatchAsync($"/api/v1/feedback/{id}", new { status = "seen" })).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Feedback_is_validated()
    {
        (await api.Admin.PostAsync("/api/v1/feedback", new { kind = "rant", message = "x" })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await api.Admin.PostAsync("/api/v1/feedback", new { kind = "idea", message = "" })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await api.Admin.PostAsync("/api/v1/feedback", new { kind = "idea", message = "ok", rating = 9 })).Status.Should().Be(HttpStatusCode.BadRequest);
    }
}
