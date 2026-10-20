using System.Net;
using System.Text.Json;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AdmissionTests(NmsApiFixture api)
{
    private async Task<Guid> LiveFormAsync()
    {
        var made = await api.Admin.PostAsync("/api/v1/forms", new { name = new { en = $"Consent {Guid.NewGuid():N}"[..14], ar = "موافقة" }, type = "admission", is_public = false });
        var id = made.Data.GetProperty("form").GetProperty("id").GetGuid();
        await api.Admin.PutAsync($"/api/v1/forms/{id}/fields", new { fields = new[] { new { id = "agree", type = "checkbox", label = new { en = "I agree" }, required = true, options = Array.Empty<object>() } } });
        await api.Admin.PostAsync($"/api/v1/forms/{id}/actions/publish");
        return id;
    }

    private async Task<Guid> StepAsync(string name, Guid? form = null, int dueDays = 7)
    {
        var made = await api.Admin.PostAsync("/api/v1/admissions/steps", new { name = new { en = $"{name} {Guid.NewGuid():N}"[..20], ar = "خطوة" }, form_id = form, due_days = dueDays });
        made.Status.Should().Be(HttpStatusCode.Created, made.Raw);
        return made.Data.EnumerateArray().OrderByDescending(s => s.GetProperty("sort_order").GetInt32()).First().GetProperty("id").GetGuid();
    }

    private static JsonElement? Current(JsonElement admission) =>
        admission.GetProperty("steps").EnumerateArray().Cast<JsonElement?>().FirstOrDefault(s => s!.Value.GetProperty("status").GetString() == "current");

    [Fact]
    public async Task Steps_are_made_changed_reordered_and_only_by_form_managers()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var form = await LiveFormAsync();
        var a = await StepAsync("Pay fee");
        var b = await StepAsync("Sign contract", form, 3);

        var denied = await teacher.Client.GetAsync("/api/v1/admissions/steps");
        var blank = await api.Admin.PostAsync("/api/v1/admissions/steps", new { name = new { ar = "x" } });
        var noForm = await api.Admin.PostAsync("/api/v1/admissions/steps", new { name = new { en = "Ghost form" }, form_id = Guid.NewGuid() });
        var all = (await api.Admin.GetAsync("/api/v1/admissions/steps")).Data.EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).ToList();
        var swapped = all.ToList();
        (swapped[^2], swapped[^1]) = (swapped[^1], swapped[^2]);
        var reorder = await api.Admin.PutAsync("/api/v1/admissions/steps/order", new { ordered_ids = swapped });
        var partial = await api.Admin.PutAsync("/api/v1/admissions/steps/order", new { ordered_ids = new[] { a } });

        denied.Status.Should().Be(HttpStatusCode.Forbidden);
        blank.Status.Should().Be(HttpStatusCode.BadRequest);
        noForm.Status.Should().Be(HttpStatusCode.BadRequest);
        reorder.Data.EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).Should().Equal(swapped);
        partial.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_child_moves_through_the_steps_the_form_step_completes_when_the_parent_submits_and_the_dashboard_adds_up()
    {
        var form = await LiveFormAsync();
        await StepAsync("Registration", form, 5);
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var child = await api.CreateStudentAsync(await api.CreateRoomAsync(), email);
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardian);

        var started = await api.Admin.PostAsync($"/api/v1/admissions/students/{child}/actions/start");
        var twice = await api.Admin.PostAsync($"/api/v1/admissions/students/{child}/actions/start");
        started.Status.Should().Be(HttpStatusCode.Created, started.Raw);
        twice.Status.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Walk the steps ahead of ours until the form step is the current one.
        var view = started.Data;
        while (Current(view) is { } now && !(now.TryGetProperty("form_id", out var f) && f.ValueKind == JsonValueKind.String && f.GetGuid() == form))
        {
            view = (await api.Admin.PostAsync($"/api/v1/admissions/students/{child}/steps/{now.GetProperty("step_id").GetGuid()}/actions/skip")).Data;
        }

        var inbox = await parent.Client.GetAsync("/api/v1/notifications?limit=50");
        var submit = await parent.Client.PostAsync($"/api/v1/forms/{form}/submissions", new { answers = new Dictionary<string, object> { ["agree"] = true }, student_id = child });
        var after = (await parent.Client.GetAsync($"/api/v1/admissions/students/{child}")).Data;
        var status = (await api.Admin.GetAsync("/api/v1/admissions/status")).Data;

        inbox.Items.Should().Contain(n => n.GetProperty("type").GetString() == "admission_form");
        submit.Status.Should().Be(HttpStatusCode.Created, submit.Raw);
        after.GetProperty("steps").EnumerateArray().First(s => s.TryGetProperty("form_id", out var f2) && f2.ValueKind == JsonValueKind.String && f2.GetGuid() == form).GetProperty("status").GetString().Should().Be("done");
        after.GetProperty("done").GetInt32().Should().BeGreaterThan(0);
        status.GetProperty("children").EnumerateArray().Should().Contain(c => c.GetProperty("student_id").GetGuid() == child);
        status.GetProperty("steps").EnumerateArray().First(s => s.GetProperty("step_id").GetGuid() != Guid.Empty).GetProperty("total").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Families_see_only_their_own_childs_progress_and_cannot_change_it()
    {
        await StepAsync("Medical records");
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var mine = await api.CreateStudentAsync(await api.CreateRoomAsync(), email);
        var theirs = await api.CreateStudentAsync(await api.CreateRoomAsync());
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{mine}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardian);
        var start = await api.Admin.PostAsync($"/api/v1/admissions/students/{mine}/actions/start");
        var step = Current(start.Data)!.Value.GetProperty("step_id").GetGuid();

        var own = await parent.Client.GetAsync($"/api/v1/admissions/students/{mine}");
        var foreign = await parent.Client.GetAsync($"/api/v1/admissions/students/{theirs}");
        var tamper = await parent.Client.PostAsync($"/api/v1/admissions/students/{mine}/steps/{step}/actions/complete");
        var dashboard = await parent.Client.GetAsync("/api/v1/admissions/status");
        var badAction = await api.Admin.PostAsync($"/api/v1/admissions/students/{mine}/steps/{step}/actions/explode");

        own.Status.Should().Be(HttpStatusCode.OK);
        foreign.Status.Should().Be(HttpStatusCode.NotFound);
        tamper.Status.Should().Be(HttpStatusCode.Forbidden);
        dashboard.Status.Should().Be(HttpStatusCode.Forbidden);
        badAction.Status.Should().Be(HttpStatusCode.BadRequest);
    }
}
