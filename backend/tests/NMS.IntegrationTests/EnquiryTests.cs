using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class EnquiryTests(NmsApiFixture api)
{
    private async Task<Guid> NewAsync(string family = "Haddad", string source = "website", string? email = null, string? dob = "2024-06-01")
    {
        var r = await api.Admin.PostAsync("/api/v1/enquiries", new
        {
            family_name = $"{family} {Guid.NewGuid():N}"[..20], child_name = "Sara Haddad", phone = "+971500000000", email = email ?? $"enq-{Guid.NewGuid():N}@test.local",
            preferred_contact = "phone", child_date_of_birth = dob, source,
        });
        r.Status.Should().Be(HttpStatusCode.Created, r.Raw);
        return r.Data.GetProperty("enquiry").GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task An_enquiry_starts_with_no_stage_and_needs_a_way_to_reach_the_family_and_a_known_source()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var id = await NewAsync();
        var noContact = await api.Admin.PostAsync("/api/v1/enquiries", new { family_name = "X", child_name = "Y", source = "website" });
        var badSource = await api.Admin.PostAsync("/api/v1/enquiries", new { family_name = "X", child_name = "Y", phone = "1", source = "billboard" });
        var denied = await teacher.Client.GetAsync("/api/v1/enquiries");
        var detail = await api.Admin.GetAsync($"/api/v1/enquiries/{id}");

        detail.Data.GetProperty("enquiry").GetProperty("stage").GetString().Should().Be("no_stage");
        detail.Data.GetProperty("history").GetArrayLength().Should().Be(1);
        noContact.Status.Should().Be(HttpStatusCode.BadRequest);
        badSource.Status.Should().Be(HttpStatusCode.BadRequest);
        denied.Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Stage_changes_are_logged_and_a_contact_moves_the_enquiry_to_contacted()
    {
        var id = await NewAsync();
        await api.Admin.PostAsync($"/api/v1/enquiries/{id}/activities", new { kind = "call", summary = "Spoke to mother, interested in full day" });
        var moved = await api.Admin.PostAsync($"/api/v1/enquiries/{id}/actions/change-stage", new { stage = "tour_appointment", reason = "Booked for Sunday" });
        var direct = await api.Admin.PostAsync($"/api/v1/enquiries/{id}/actions/change-stage", new { stage = "enrolled" });
        var unknown = await api.Admin.PostAsync($"/api/v1/enquiries/{id}/actions/change-stage", new { stage = "dreaming" });

        var history = moved.Data.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("to_stage").GetString()).ToList();
        history.Should().Equal("no_stage", "contacted", "tour_appointment");
        moved.Data.GetProperty("activities").GetArrayLength().Should().Be(1);
        moved.Data.GetProperty("enquiry").GetProperty("last_contact_date").ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null);
        direct.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        unknown.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Converting_creates_a_signed_up_child_and_parent_links_back_and_cannot_be_repeated()
    {
        var id = await NewAsync(source: "referral");
        var noDob = await NewAsync(dob: null);
        var refused = await api.Admin.PostAsync($"/api/v1/enquiries/{noDob}/actions/convert", new { });
        var done = await api.Admin.PostAsync($"/api/v1/enquiries/{id}/actions/convert", new { });
        var again = await api.Admin.PostAsync($"/api/v1/enquiries/{id}/actions/convert", new { });

        done.Status.Should().Be(HttpStatusCode.OK, done.Raw);
        var student = done.Data.GetProperty("enquiry").GetProperty("student_id").GetGuid();
        var child = await api.Admin.GetAsync($"/api/v1/students/{student}");
        done.Data.GetProperty("enquiry").GetProperty("stage").GetString().Should().Be("enrolled");
        child.Data.GetProperty("status").GetString().Should().Be("signed_up");
        refused.Status.Should().Be(HttpStatusCode.BadRequest);
        again.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task The_list_filters_by_stage_and_source_hides_archived_and_the_summary_counts_sources_that_enrol()
    {
        var a = await NewAsync(source: "social_media");
        var b = await NewAsync(source: "social_media");
        await api.Admin.PostAsync($"/api/v1/enquiries/{b}/actions/archive");
        await api.Admin.PostAsync($"/api/v1/enquiries/{a}/actions/convert", new { });

        var enrolled = await api.Admin.GetAsync("/api/v1/enquiries?stage=enrolled&source=social_media&limit=100");
        var archived = await api.Admin.GetAsync("/api/v1/enquiries?archived=true&limit=100");
        var summary = await api.Admin.GetAsync("/api/v1/enquiries/summary");

        enrolled.Items.Select(i => i.GetProperty("id").GetGuid()).Should().Contain(a).And.NotContain(b);
        archived.Items.Select(i => i.GetProperty("id").GetGuid()).Should().Contain(b);
        summary.Data.GetProperty("by_stage").GetArrayLength().Should().Be(6);
        summary.Data.GetProperty("by_source").EnumerateArray().First(s => s.GetProperty("source").GetString() == "social_media").GetProperty("enrolled").GetInt32().Should().BeGreaterThan(0);
    }
}
