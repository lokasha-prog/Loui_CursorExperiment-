using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SettingsTests(NmsApiFixture api)
{
    private object Centre(string name = "Bright Futures Test", int late = 15, bool invoices = true, string? colour = "#0E7C86") => new
    {
        name = new { en = name, ar = "برايت فيوتشرز" }, contact_email = "hello@centre.test", contact_phone = "+97140000000", khda_license_number = "KH-123",
        weekend_days = new[] { "fri", "sat" }, address = "Dubai", website = "https://centre.test", brand_color = colour,
        late_grace_minutes = late, early_leave_grace_minutes = 10, parents_see_invoices = invoices,
    };

    [Fact]
    public async Task Centre_settings_are_saved_checked_and_for_settings_roles_only()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var saved = await api.Admin.PutAsync("/api/v1/settings/centre", Centre(late: 20));
        var read = await api.Admin.GetAsync("/api/v1/settings/centre");
        var badColour = await api.Admin.PutAsync("/api/v1/settings/centre", Centre(colour: "teal"));
        var badGrace = await api.Admin.PutAsync("/api/v1/settings/centre", Centre(late: 500));
        var denied = await teacher.Client.PutAsync("/api/v1/settings/centre", Centre());
        await api.Admin.PutAsync("/api/v1/settings/centre", Centre());

        saved.Status.Should().Be(HttpStatusCode.OK, saved.Raw);
        read.Data.GetProperty("brand_color").GetString().Should().Be("#0E7C86");
        read.Data.GetProperty("late_grace_minutes").GetInt32().Should().Be(20);
        badColour.Status.Should().Be(HttpStatusCode.BadRequest);
        badGrace.Status.Should().Be(HttpStatusCode.BadRequest);
        denied.Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Switching_off_parent_invoices_hides_them_from_families_only()
    {
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var child = await api.CreateStudentAsync(await api.CreateRoomAsync(), email);
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardian);
        await api.Admin.PostAsync("/api/v1/invoices", new
        {
            student_id = child, period_start = "2026-10-01", period_end = "2026-10-31", issue = true,
            lines = new[] { new { description = new { en = "Fees" }, quantity = 1, unit_price_fils = 10_000, tax_rate_percent = (decimal?)null } },
        });

        var before = await parent.Client.GetAsync("/api/v1/invoices");
        await api.Admin.PutAsync("/api/v1/settings/centre", Centre(invoices: false));
        var hidden = await parent.Client.GetAsync("/api/v1/invoices");
        var staff = await api.Admin.GetAsync($"/api/v1/invoices?student_id={child}");
        await api.Admin.PutAsync("/api/v1/settings/centre", Centre());

        before.Items.Should().HaveCount(1);
        hidden.Items.Should().BeEmpty();
        staff.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_custom_role_gives_exactly_the_chosen_permissions_and_cannot_hand_out_account_management()
    {
        var made = await api.Admin.PostAsync("/api/v1/roles", new { display_name = new { en = $"Open Day Helper {Guid.NewGuid():N}"[..28] }, permissions = new[] { "enquiries.read", "tours.read" } });
        made.Status.Should().Be(HttpStatusCode.Created, made.Raw);
        var role = made.Data.GetProperty("name").GetString()!;
        var user = await api.CreateUserAsync(role);

        var allowed = await user.Client.GetAsync("/api/v1/enquiries");
        var notAllowed = await user.Client.GetAsync("/api/v1/expenses");
        var cannotWrite = await user.Client.PostAsync("/api/v1/enquiries", new { family_name = "A", child_name = "B", phone = "1", source = "walk_in" });
        var me = await user.Client.GetAsync("/api/v1/auth/me");
        var escalate = await api.Admin.PostAsync("/api/v1/roles", new { display_name = new { en = "Sneaky" }, permissions = new[] { "user_management.manage" } });
        var catalogue = await api.Admin.GetAsync("/api/v1/roles/permissions");

        allowed.Status.Should().Be(HttpStatusCode.OK);
        notAllowed.Status.Should().Be(HttpStatusCode.Forbidden);
        cannotWrite.Status.Should().Be(HttpStatusCode.Forbidden);
        me.Data.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).Should().Contain("enquiries.read").And.NotContain("enquiries.write");
        escalate.Status.Should().Be(HttpStatusCode.BadRequest);
        catalogue.Raw.Should().NotContain("user_management.manage");
    }

    [Fact]
    public async Task Built_in_roles_are_protected_and_a_role_in_use_cannot_be_deleted()
    {
        var roles = await api.Admin.GetAsync("/api/v1/roles");
        var teacherRole = roles.Data.EnumerateArray().First(r => r.GetProperty("name").GetString() == "teacher");
        var edit = await api.Admin.PatchAsync($"/api/v1/roles/{teacherRole.GetProperty("id").GetGuid()}", new { display_name = new { en = "Teacher" }, permissions = new[] { "students.read" } });
        var del = await api.Admin.DeleteAsync($"/api/v1/roles/{teacherRole.GetProperty("id").GetGuid()}");

        var made = await api.Admin.PostAsync("/api/v1/roles", new { display_name = new { en = $"Temp {Guid.NewGuid():N}"[..20] }, permissions = new[] { "students.read" } });
        var id = made.Data.GetProperty("id").GetGuid();
        await api.CreateUserAsync(made.Data.GetProperty("name").GetString()!);
        var inUse = await api.Admin.DeleteAsync($"/api/v1/roles/{id}");
        var free = await api.Admin.PostAsync("/api/v1/roles", new { display_name = new { en = $"Spare {Guid.NewGuid():N}"[..20] }, permissions = new[] { "students.read" } });
        var gone = await api.Admin.DeleteAsync($"/api/v1/roles/{free.Data.GetProperty("id").GetGuid()}");

        edit.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        del.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        inUse.Status.Should().Be(HttpStatusCode.Conflict);
        gone.Status.Should().Be(HttpStatusCode.NoContent);
    }
}
