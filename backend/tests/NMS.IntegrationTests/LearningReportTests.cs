using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class LearningReportTests(NmsApiFixture api)
{
    private record Setup(Guid Fw, Guid Template, Guid Room, Guid Child, Guid Run, ApiClient Parent);

    private async Task<Setup> ArrangeAsync(string term = "Autumn")
    {
        var code = $"fw-{Guid.NewGuid():N}"[..12];
        var fw = (await api.Admin.PostAsync("/api/v1/learning/frameworks", new { code, name = new { en = "Test" } })).Data.GetProperty("id").GetGuid();
        var tree = await api.Admin.PostAsync($"/api/v1/learning/frameworks/{fw}/import", new
        {
            areas = new[] { new { code = "cal", name = new { en = "Communication" }, sort_order = 1 } },
            age_bands = new[] { new { code = "b1", name = new { en = "Band" }, min_months = 8, max_months = 60 } },
            milestones = new[] { new { area = "cal", age_band = "b1", code = "m1", statement = new { en = "Says two words" } }, new { area = "cal", age_band = "b1", code = "m2", statement = new { en = "Follows a request" } } },
        });
        var ms = tree.Data.GetProperty("milestones").EnumerateArray().Select(m => m.GetProperty("id").GetGuid()).ToArray();
        var template = (await api.Admin.PostAsync("/api/v1/learning/assessment-templates", new { name = new { en = "Check" }, framework_id = fw, scoring_kind = "three_point", milestone_ids = ms })).Data.GetProperty("id").GetGuid();
        var room = await api.CreateRoomAsync();
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var child = await api.CreateStudentAsync(room, email);
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = (await api.CreateUserAsync("parent", guardianId: guardian)).Client;
        var run = (await api.Admin.PostAsync("/api/v1/learning/assessments", new { template_id = template, student_id = child, term })).Data.GetProperty("id").GetGuid();
        await api.Admin.PutAsync($"/api/v1/learning/assessments/{run}/scores", new
        {
            scores = new[] { new { milestone_id = ms[0], score = 2, note = "Said mama and ball" }, new { milestone_id = ms[1], score = 1, note = "" } },
            staff_summary = new { en = "Clinical detail" }, parent_summary = new { en = "Growing in confidence." },
        });
        await api.Admin.PostAsync($"/api/v1/learning/assessments/{run}/actions/complete");
        return new Setup(fw, template, room, child, run, parent);
    }

    private static Task<bool> IsPdfAsync((HttpStatusCode Status, byte[] Bytes, string? ContentType) r) => Task.FromResult(r.Status == HttpStatusCode.OK && r.Bytes.Length > 4 && System.Text.Encoding.ASCII.GetString(r.Bytes, 0, 4) == "%PDF");

    [Fact]
    public async Task Every_report_can_be_made_as_a_pdf_for_staff_in_both_languages()
    {
        var s = await ArrangeAsync();
        await api.Admin.PostAsync($"/api/v1/learning/assessments/{s.Run}/actions/approve");
        await ArrangeSecondTermAsync(s);
        var urls = new[]
        {
            $"progress?student_id={s.Child}", $"snapshot?run_id={s.Run}", $"comparison?template_id={s.Template}&term_a=Autumn&term_b=Spring&room_id={s.Room}",
            $"class_milestone?room_id={s.Room}&template_id={s.Template}", $"student_milestone?student_id={s.Child}", $"journey?student_id={s.Child}",
        };
        foreach (var lang in new[] { "en", "ar" })
        {
            foreach (var u in urls)
            {
                var reply = await api.Admin.GetBytesAsync($"/api/v1/learning/reports/{u}&variant=staff&language={lang}&format=pdf");
                (await IsPdfAsync(reply)).Should().BeTrue($"{u} {lang}: {reply.Status}");
            }
        }
    }

    private async Task ArrangeSecondTermAsync(Setup s)
    {
        var run = (await api.Admin.PostAsync("/api/v1/learning/assessments", new { template_id = s.Template, student_id = s.Child, term = "Spring" })).Data.GetProperty("id").GetGuid();
        await api.Admin.PostAsync($"/api/v1/learning/assessments/{run}/actions/complete");
    }

    [Fact]
    public async Task The_family_version_shows_levels_only_and_only_after_approval_and_never_to_the_wrong_person()
    {
        var s = await ArrangeAsync();
        var before = await s.Parent.GetBytesAsync($"/api/v1/learning/reports/snapshot?run_id={s.Run}&variant=parent&format=pdf");
        await api.Admin.PostAsync($"/api/v1/learning/assessments/{s.Run}/actions/approve");
        var after = await s.Parent.GetBytesAsync($"/api/v1/learning/reports/snapshot?run_id={s.Run}&variant=parent&format=pdf");
        var asStaffView = await s.Parent.GetBytesAsync($"/api/v1/learning/reports/snapshot?run_id={s.Run}&variant=staff&format=pdf");
        var clinical = await s.Parent.GetBytesAsync($"/api/v1/learning/reports/comparison?template_id={s.Template}&term_a=a&term_b=b&variant=parent&format=pdf");
        var other = await ArrangeAsync("Other");
        var wrongFamily = await other.Parent.GetBytesAsync($"/api/v1/learning/reports/snapshot?run_id={s.Run}&variant=parent&format=pdf");
        var json = await s.Parent.GetBytesAsync($"/api/v1/learning/reports/snapshot?run_id={s.Run}&variant=parent&format=json");

        before.Status.Should().Be(HttpStatusCode.NotFound);
        (await IsPdfAsync(after)).Should().BeTrue();
        asStaffView.Status.Should().Be(HttpStatusCode.Forbidden);
        clinical.Status.Should().Be(HttpStatusCode.BadRequest);
        wrongFamily.Status.Should().Be(HttpStatusCode.NotFound);
        json.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task What_staff_print_for_a_family_leaves_out_scores_notes_codes_and_concerns()
    {
        var s = await ArrangeAsync();
        await api.Admin.PostAsync($"/api/v1/learning/assessments/{s.Run}/actions/approve");
        await api.Admin.PostAsync("/api/v1/learning/observations", new { student_id = s.Child, type = "concern", share_with_parents = true, notes = new { en = "Worried about speech" } });
        await api.Admin.PostAsync("/api/v1/learning/observations", new { student_id = s.Child, type = "informal", share_with_parents = true, notes = new { en = "Built a tall tower" } });

        var staffJson = await api.Admin.GetBytesAsync($"/api/v1/learning/reports/snapshot?run_id={s.Run}&variant=staff&format=json");
        var familyJson = await api.Admin.GetBytesAsync($"/api/v1/learning/reports/snapshot?run_id={s.Run}&variant=parent&format=json");
        var journeyFamily = await api.Admin.GetBytesAsync($"/api/v1/learning/reports/journey?student_id={s.Child}&variant=parent&format=json");

        var staffText = System.Text.Encoding.UTF8.GetString(staffJson.Bytes);
        var familyText = System.Text.Encoding.UTF8.GetString(familyJson.Bytes);
        var journeyText = System.Text.Encoding.UTF8.GetString(journeyFamily.Bytes);
        staffText.Should().Contain("Said mama and ball").And.Contain("Clinical detail");
        familyText.Should().Contain("Growing in confidence").And.NotContain("mama").And.NotContain("Clinical").And.NotContain("m1");
        journeyText.Should().Contain("Built a tall tower").And.NotContain("Worried");
    }
}
