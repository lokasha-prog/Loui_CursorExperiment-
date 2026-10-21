using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AssessmentTests(NmsApiFixture api)
{
    private async Task<(Guid Fw, Guid Area, Guid Band, Guid M1, Guid M2)> FrameworkAsync()
    {
        var code = $"fw-{Guid.NewGuid():N}"[..12];
        var fw = (await api.Admin.PostAsync("/api/v1/learning/frameworks", new { code, name = new { en = "Test" } })).Data.GetProperty("id").GetGuid();
        var tree = await api.Admin.PostAsync($"/api/v1/learning/frameworks/{fw}/import", new
        {
            areas = new[] { new { code = "cal", name = new { en = "Communication" }, sort_order = 1 } },
            age_bands = new[] { new { code = "b1", name = new { en = "Band" }, min_months = 8, max_months = 20 } },
            milestones = new[]
            {
                new { area = "cal", age_band = "b1", code = "m1", statement = new { en = "Says two words" } },
                new { area = "cal", age_band = "b1", code = "m2", statement = new { en = "Follows a request" } },
            },
        });
        return (fw, tree.Data.GetProperty("areas")[0].GetProperty("id").GetGuid(), tree.Data.GetProperty("age_bands")[0].GetProperty("id").GetGuid(),
            tree.Data.GetProperty("milestones")[0].GetProperty("id").GetGuid(), tree.Data.GetProperty("milestones")[1].GetProperty("id").GetGuid());
    }

    private async Task<(ApiClient Parent, Guid Child)> FamilyAsync()
    {
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var child = await api.CreateStudentAsync(await api.CreateRoomAsync(), email);
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        return ((await api.CreateUserAsync("parent", guardianId: guardian)).Client, child);
    }

    [Fact]
    public async Task The_learning_journey_shows_a_family_only_what_was_shared_and_never_a_concern()
    {
        var f = await FrameworkAsync();
        var (parent, child) = await FamilyAsync();
        async Task Obs(string type, bool share, string text) => (await api.Admin.PostAsync("/api/v1/learning/observations", new
        {
            student_id = child, type, framework_id = f.Fw, area_id = f.Area, age_band_id = f.Band, milestone_ids = new[] { f.M1 }, share_with_parents = share, notes = new { en = text },
        })).Status.Should().Be(HttpStatusCode.Created);
        await Obs("informal", true, "Built a tall tower");
        await Obs("informal", false, "Staff only note");
        await Obs("concern", true, "Worried about speech");

        var staff = await api.Admin.GetAsync($"/api/v1/learning/journey/{child}");
        var family = await parent.GetAsync($"/api/v1/learning/journey/{child}");

        staff.Data.GetProperty("items").GetArrayLength().Should().Be(3);
        family.Data.GetProperty("items").GetArrayLength().Should().Be(1);
        family.Raw.Should().Contain("Built a tall tower").And.NotContain("Worried").And.NotContain("Staff only");
        (await parent.GetAsync("/api/v1/learning/observations")).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_concern_alerts_the_managers_and_cannot_be_shared()
    {
        var (_, child) = await FamilyAsync();
        var manager = await api.CreateUserAsync("centre_admin");

        var made = await api.Admin.PostAsync("/api/v1/learning/observations", new { student_id = child, type = "concern", share_with_parents = true, notes = new { en = "Needs a closer look" } });
        var alerts = await manager.Client.GetAsync("/api/v1/notifications?limit=20");

        made.Data.GetProperty("share_with_parents").GetBoolean().Should().BeFalse();
        alerts.Raw.Should().Contain("learning_concern");
    }

    [Fact]
    public async Task An_assessment_is_scored_completed_and_shown_to_the_family_only_after_a_manager_approves_a_plain_version()
    {
        var f = await FrameworkAsync();
        var (parent, child) = await FamilyAsync();
        var template = await api.Admin.PostAsync("/api/v1/learning/assessment-templates", new { name = new { en = "Toddler check" }, framework_id = f.Fw, age_band_id = f.Band, scoring_kind = "three_point", milestone_ids = new[] { f.M1, f.M2 } });
        template.Status.Should().Be(HttpStatusCode.Created, template.Raw);
        var templateId = template.Data.GetProperty("id").GetGuid();

        var run = await api.Admin.PostAsync("/api/v1/learning/assessments", new { template_id = templateId, student_id = child, term = "Autumn 2026" });
        var again = await api.Admin.PostAsync("/api/v1/learning/assessments", new { template_id = templateId, student_id = child, term = "Autumn 2026" });
        var runId = run.Data.GetProperty("id").GetGuid();
        var tooHigh = await api.Admin.PutAsync($"/api/v1/learning/assessments/{runId}/scores", new { scores = new[] { new { milestone_id = f.M1, score = 5 } } });
        var scored = await api.Admin.PutAsync($"/api/v1/learning/assessments/{runId}/scores", new
        {
            scores = new[] { new { milestone_id = f.M1, score = 2, note = "Said mama and ball" }, new { milestone_id = f.M2, score = 1, note = "" } },
            staff_summary = new { en = "Clinical detail" }, parent_summary = new { en = "Zayed is chatting more every week." },
        });
        var early = await api.Admin.PostAsync($"/api/v1/learning/assessments/{runId}/actions/approve");
        await api.Admin.PostAsync($"/api/v1/learning/assessments/{runId}/actions/complete");
        var beforeApproval = await parent.GetAsync($"/api/v1/learning/parent-reports/{child}");
        var approved = await api.Admin.PostAsync($"/api/v1/learning/assessments/{runId}/actions/approve");
        var afterApproval = await parent.GetAsync($"/api/v1/learning/parent-reports/{child}");

        run.Status.Should().Be(HttpStatusCode.Created);
        again.Status.Should().Be(HttpStatusCode.Conflict);
        tooHigh.Status.Should().Be(HttpStatusCode.BadRequest);
        scored.Data.GetProperty("progress_percent").GetInt32().Should().Be(75);
        early.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        beforeApproval.Data.GetArrayLength().Should().Be(0);
        approved.Data.GetProperty("parent_approved").GetBoolean().Should().BeTrue();
        afterApproval.Data.GetArrayLength().Should().Be(1);
        afterApproval.Data[0].GetProperty("areas")[0].GetProperty("level").GetString().Should().Be("secure");
        afterApproval.Raw.Should().Contain("chatting more").And.NotContain("Clinical").And.NotContain("mama").And.NotContain("\"code\"").And.NotContain("\"score\"");
    }

    [Fact]
    public async Task Changing_scores_after_approval_withdraws_the_parent_version_until_it_is_approved_again()
    {
        var f = await FrameworkAsync();
        var (parent, child) = await FamilyAsync();
        var templateId = (await api.Admin.PostAsync("/api/v1/learning/assessment-templates", new { name = new { en = "Check" }, framework_id = f.Fw, scoring_kind = "binary", milestone_ids = new[] { f.M1 } })).Data.GetProperty("id").GetGuid();
        var runId = (await api.Admin.PostAsync("/api/v1/learning/assessments", new { template_id = templateId, student_id = child, term = "T1" })).Data.GetProperty("id").GetGuid();
        await api.Admin.PutAsync($"/api/v1/learning/assessments/{runId}/scores", new { scores = new[] { new { milestone_id = f.M1, score = 1 } }, parent_summary = new { en = "Doing well." } });
        await api.Admin.PostAsync($"/api/v1/learning/assessments/{runId}/actions/complete");
        await api.Admin.PostAsync($"/api/v1/learning/assessments/{runId}/actions/approve");

        await api.Admin.PutAsync($"/api/v1/learning/assessments/{runId}/scores", new { scores = new[] { new { milestone_id = f.M1, score = 0 } } });
        var hidden = await parent.GetAsync($"/api/v1/learning/parent-reports/{child}");
        var deleteDraft = await api.Admin.DeleteAsync($"/api/v1/learning/assessments/{runId}");

        hidden.Data.GetArrayLength().Should().Be(0);
        deleteDraft.Status.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_teacher_cannot_define_templates_and_cannot_see_a_child_outside_their_rooms()
    {
        var f = await FrameworkAsync();
        var (_, child) = await FamilyAsync();
        var teacher = await api.CreateUserAsync("teacher");

        var template = await teacher.Client.PostAsync("/api/v1/learning/assessment-templates", new { name = new { en = "x" }, framework_id = f.Fw, milestone_ids = new[] { f.M1 } });
        var observe = await teacher.Client.PostAsync("/api/v1/learning/observations", new { student_id = child, type = "informal", notes = new { en = "x" } });
        var journey = await teacher.Client.GetAsync($"/api/v1/learning/journey/{child}");

        template.Status.Should().Be(HttpStatusCode.Forbidden);
        observe.Status.Should().Be(HttpStatusCode.BadRequest);
        journey.Status.Should().Be(HttpStatusCode.NotFound);
    }
}
