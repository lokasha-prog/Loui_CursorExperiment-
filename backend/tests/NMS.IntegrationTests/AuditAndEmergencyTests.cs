using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuditAndEmergencyTests(NmsApiFixture api)
{
    private async Task<(Guid Child, Guid Room)> ChildAsync(string? medical = "Asthma, inhaler in bag")
    {
        var room = await api.CreateRoomAsync();
        var child = await api.CreateStudentAsync(room, $"fam-{Guid.NewGuid():N}@test.local", medical: medical);
        await api.Admin.PostAsync($"/api/v1/students/{child}/actions/transition-status", new { status = "active" });
        return (child, room);
    }

    [Fact]
    public async Task The_audit_log_viewer_is_for_super_administrators_and_filters_by_action_and_date()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var manager = await api.CreateUserAsync("centre_admin");
        await ChildAsync();
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var all = await api.Admin.GetAsync($"/api/v1/audit-logs?action=create&from={today}&to={today}&limit=100");
        var wrongDay = await api.Admin.GetAsync("/api/v1/audit-logs?from=2001-01-01&to=2001-01-02");
        var backwards = await api.Admin.GetAsync("/api/v1/audit-logs?from=2026-02-01&to=2026-01-01");

        all.Status.Should().Be(HttpStatusCode.OK, all.Raw);
        all.Items.Should().NotBeEmpty().And.OnlyContain(l => l.GetProperty("action").GetString() == "create");
        all.Items.Should().Contain(l => l.GetProperty("entity_type").GetString() == "Student");
        wrongDay.Items.Should().BeEmpty();
        backwards.Status.Should().Be(HttpStatusCode.BadRequest);
        (await teacher.Client.GetAsync("/api/v1/audit-logs")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await manager.Client.GetAsync("/api/v1/audit-logs")).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_audit_log_never_shows_medical_details_in_clear()
    {
        await ChildAsync("Secret diagnosis XYZ");

        var logs = await api.Admin.GetAsync("/api/v1/audit-logs?entity_type=Student&limit=100");

        logs.Raw.Should().NotContain("Secret diagnosis XYZ");
    }

    [Fact]
    public async Task A_child_emergency_sheet_is_a_real_pdf_in_either_language_and_printing_it_is_recorded()
    {
        var (child, _) = await ChildAsync();

        var english = await api.Admin.GetBytesAsync($"/api/v1/students/{child}/emergency-report");
        var arabic = await api.Admin.GetBytesAsync($"/api/v1/students/{child}/emergency-report?language=ar");
        var bad = await api.Admin.GetBytesAsync($"/api/v1/students/{child}/emergency-report?language=fr");
        var logs = await api.Admin.GetAsync("/api/v1/audit-logs?action=export&limit=50");

        english.Status.Should().Be(HttpStatusCode.OK);
        english.ContentType.Should().Be("application/pdf");
        System.Text.Encoding.ASCII.GetString(english.Bytes, 0, 4).Should().Be("%PDF");
        arabic.Bytes.Length.Should().BeGreaterThan(1000);
        bad.Status.Should().Be(HttpStatusCode.BadRequest);
        logs.Items.Should().Contain(l => l.GetProperty("entity_id").GetGuid() == child);
    }

    [Fact]
    public async Task A_room_sheet_has_a_page_per_child_and_only_the_rooms_teacher_may_print_it()
    {
        var (_, room) = await ChildAsync();
        var second = await api.CreateStudentAsync(room, null);
        await api.Admin.PostAsync($"/api/v1/students/{second}/actions/transition-status", new { status = "active" });
        var staff = await api.CreateStaffAsync();
        await api.AssignStaffToRoomAsync(staff, room);
        var mine = await api.CreateUserAsync("teacher", staffId: staff);
        var outsider = await api.CreateUserAsync("teacher");
        var parentEmail = $"p-{Guid.NewGuid():N}@test.local";
        var (child, _) = await ChildAsync();
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardian);

        var sheet = await mine.Client.GetBytesAsync($"/api/v1/rooms/{room}/emergency-report");
        var denied = await outsider.Client.GetBytesAsync($"/api/v1/rooms/{room}/emergency-report");
        var forParent = await parent.Client.GetBytesAsync($"/api/v1/students/{child}/emergency-report");

        sheet.Status.Should().Be(HttpStatusCode.OK);
        System.Text.RegularExpressions.Regex.Matches(System.Text.Encoding.Latin1.GetString(sheet.Bytes), "/Type /Page\\b").Count.Should().BeGreaterThanOrEqualTo(2);
        denied.Status.Should().Be(HttpStatusCode.NotFound);
        forParent.Status.Should().Be(HttpStatusCode.Forbidden);
        parentEmail.Should().NotBeNull();
    }
}
