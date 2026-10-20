using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AiBrainTests(NmsApiFixture api)
{
    [Fact]
    public async Task The_summary_is_built_from_totals_only_and_never_contains_names_or_ids()
    {
        var room = await api.CreateRoomAsync();
        var id = await api.CreateStudentAsync(room, "secret-guardian@test.local", first: "Zubaidah");
        await api.Admin.PostAsync($"/api/v1/students/{id}/actions/transition-status", new { status = "active" });
        var barcode = (await api.Admin.GetAsync($"/api/v1/students/{id}")).Data.GetProperty("barcode").GetString()!;
        await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode });

        var reply = await api.Admin.GetAsync("/api/v1/ai/attendance-summary?days=7&language=ar");

        reply.Status.Should().Be(HttpStatusCode.OK, reply.Raw);
        reply.Data.GetProperty("summary").GetString().Should().Be("Attendance was steady.");
        reply.Data.GetProperty("figures").GetArrayLength().Should().Be(7);
        reply.Data.GetProperty("model").GetString().Should().Be("fake-model");
        var sent = api.Ai.LastUser + api.Ai.LastSystem;
        sent.Should().NotContain("Zubaidah").And.NotContain("Family").And.NotContain("secret-guardian").And.NotContain(id.ToString())
            .And.NotContain(barcode);
        api.Ai.LastSystem.Should().Contain("Arabic");
        api.Ai.LastUser.Should().Contain("Period: last 7 days");
    }

    [Fact]
    public async Task Only_managers_can_ask_and_the_range_is_limited()
    {
        var teacher = await api.CreateUserAsync("teacher");

        var denied = await teacher.Client.GetAsync("/api/v1/ai/attendance-summary");
        var tooLong = await api.Admin.GetAsync("/api/v1/ai/attendance-summary?days=500");
        var badLanguage = await api.Admin.GetAsync("/api/v1/ai/attendance-summary?language=fr");

        denied.Status.Should().Be(HttpStatusCode.Forbidden);
        tooLong.Status.Should().Be(HttpStatusCode.BadRequest);
        badLanguage.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task When_the_brain_is_not_set_up_or_down_the_answer_is_a_clear_refusal_not_a_crash()
    {
        api.Ai.IsConfigured = false;
        try
        {
            var notSetUp = await api.Admin.GetAsync("/api/v1/ai/attendance-summary");
            api.Ai.IsConfigured = true;
            api.Ai.Answer = NMS.Shared.Results.Error.InvalidState("The AI Brain is not available right now.");
            var down = await api.Admin.GetAsync("/api/v1/ai/attendance-summary");

            notSetUp.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
            down.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        }
        finally
        {
            api.Ai.IsConfigured = true;
            api.Ai.Answer = NMS.Shared.Results.Result<string>.Success("Attendance was steady.");
        }
    }
}
