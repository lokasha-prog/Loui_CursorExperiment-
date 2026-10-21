using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ManagementBriefingTests(NmsApiFixture api)
{
    [Theory]
    [InlineData("overview", "Active children")]
    [InlineData("finance", "Collection rate this month")]
    [InlineData("admissions", "Enquiries in the last 90 days")]
    [InlineData("people", "Active staff")]
    public async Task Each_briefing_is_built_from_totals_only_and_returns_the_figures_it_used(string area, string expectedFact)
    {
        var room = await api.CreateRoomAsync();
        var id = await api.CreateStudentAsync(room, "private-parent@test.local", first: "Maitha");
        await api.Admin.PostAsync($"/api/v1/students/{id}/actions/transition-status", new { status = "active" });

        var reply = await api.Admin.GetAsync($"/api/v1/ai/briefing/{area}?language=en");

        reply.Status.Should().Be(HttpStatusCode.OK, reply.Raw);
        reply.Data.GetProperty("area").GetString().Should().Be(area);
        reply.Data.GetProperty("summary").GetString().Should().NotBeNullOrWhiteSpace();
        reply.Data.GetProperty("facts").EnumerateArray().Should().Contain(f => f.GetProperty("label").GetString() == expectedFact);
        (api.Ai.LastUser + api.Ai.LastSystem).Should().NotContain("Maitha").And.NotContain("private-parent").And.NotContain(id.ToString());
    }

    [Fact]
    public async Task Each_area_needs_the_permission_for_its_data_and_bad_input_is_refused()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var accounts = await api.CreateUserAsync("accounts");

        (await teacher.Client.GetAsync("/api/v1/ai/briefing/finance")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await teacher.Client.GetAsync("/api/v1/ai/briefing/overview")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await accounts.Client.GetAsync("/api/v1/ai/briefing/finance")).Status.Should().Be(HttpStatusCode.OK);
        (await accounts.Client.GetAsync("/api/v1/ai/briefing/admissions")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await api.Admin.GetAsync("/api/v1/ai/briefing/finance?language=fr")).Status.Should().Be(HttpStatusCode.BadRequest);
    }
}
