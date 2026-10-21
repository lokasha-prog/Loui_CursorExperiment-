using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CentreGroupTests(NmsApiFixture api)
{
    private async Task<(Guid Id, string Slug)> NewBranchAsync()
    {
        var slug = $"branch-{Guid.NewGuid():N}"[..20];
        var made = await api.Admin.PostAsync("/api/v1/group/centres", new { name = "Branch Centre", name_arabic = "فرع", slug });
        made.Status.Should().Be(HttpStatusCode.Created, made.Raw);
        return (made.Data.GetProperty("id").GetGuid(), slug);
    }

    private async Task<ApiClient> BranchClientAsync(Guid id)
    {
        var switched = await api.Admin.PostAsync("/api/v1/group/switch", new { centre_id = id });
        switched.Status.Should().Be(HttpStatusCode.OK, switched.Raw);
        var branch = api.NewClient();
        branch.SetBearer(switched.Data.GetProperty("access_token").GetString()!);
        return branch;
    }

    [Fact]
    public async Task The_group_owner_adds_a_centre_signs_in_once_and_switches_to_it()
    {
        var (id, slug) = await NewBranchAsync();

        var mine = await api.Admin.GetAsync("/api/v1/group/my-centres");
        mine.Items.Select(c => c.GetProperty("id").GetGuid()).Should().Contain([api.TenantId, id]);

        var switched = await api.Admin.PostAsync("/api/v1/group/switch", new { centre_id = id });
        switched.Status.Should().Be(HttpStatusCode.OK, switched.Raw);
        switched.Data.GetProperty("me").GetProperty("tenant").GetProperty("slug").GetString().Should().Be(slug);
    }

    [Fact]
    public async Task A_slug_already_in_use_and_a_branch_adding_a_branch_are_refused()
    {
        var (id, slug) = await NewBranchAsync();
        (await api.Admin.PostAsync("/api/v1/group/centres", new { name = "Again", slug })).Status.Should().Be(HttpStatusCode.Conflict);

        var branch = await BranchClientAsync(id);
        (await branch.PostAsync("/api/v1/group/centres", new { name = "Nested", slug = $"nested-{Guid.NewGuid():N}"[..20] })).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Staff_and_other_people_cannot_see_group_figures()
    {
        var teacher = await api.CreateUserAsync("teacher");
        (await teacher.Client.GetAsync("/api/v1/group/overview")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await teacher.Client.PostAsync("/api/v1/group/centres", new { name = "X", slug = "xxx-yyy" })).Status.Should().Be(HttpStatusCode.Forbidden);
        (await teacher.Client.PostAsync("/api/v1/group/switch", new { centre_id = Guid.NewGuid() })).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_group_dashboard_and_reports_cover_every_centre_and_a_branch_cannot_read_the_head_data()
    {
        var (id, _) = await NewBranchAsync();
        var child = await api.CreateStudentAsync();
        (await api.Admin.PostAsync($"/api/v1/students/{child}/actions/transition-status", new { status = "active" })).Status.Should().Be(HttpStatusCode.OK);

        var overview = await api.Admin.GetAsync("/api/v1/group/overview");
        overview.Status.Should().Be(HttpStatusCode.OK, overview.Raw);
        var centres = overview.Data.GetProperty("centres").EnumerateArray().ToList();
        centres.Should().Contain(c => c.GetProperty("centre_id").GetGuid() == id);
        centres.First(c => c.GetProperty("centre_id").GetGuid() == id).GetProperty("students").GetInt32().Should().Be(0);
        overview.Data.GetProperty("total").GetProperty("students").GetInt32().Should()
            .Be(centres.Sum(c => c.GetProperty("students").GetInt32()));

        foreach (var kind in new[] { "attendance", "revenue", "enrolment" })
        {
            var report = await api.Admin.GetAsync($"/api/v1/group/reports/{kind}?lang=ar");
            report.Status.Should().Be(HttpStatusCode.OK, report.Raw);
            report.Data.GetProperty("rows").GetArrayLength().Should().Be(centres.Count + 1);
        }

        (await api.Admin.GetAsync("/api/v1/group/reports/nonsense")).Status.Should().Be(HttpStatusCode.BadRequest);
        (await api.Admin.GetAsync("/api/v1/group/reports/revenue?format=excel")).Status.Should().Be(HttpStatusCode.OK);

        var branch = await BranchClientAsync(id);
        (await branch.GetAsync($"/api/v1/students/{child}")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_child_moves_to_another_centre_with_family_and_both_sides_keep_a_record()
    {
        var (id, _) = await NewBranchAsync();
        var parent = $"mum-{Guid.NewGuid():N}@test.local";
        var child = await api.CreateStudentAsync(guardianEmail: parent, medical: "Peanut allergy");
        await api.Admin.PostAsync($"/api/v1/students/{child}/actions/transition-status", new { status = "active" });

        var moved = await api.Admin.PostAsync($"/api/v1/group/students/{child}/transfer", new { to_centre_id = id, reason = "Family moved" });
        moved.Status.Should().Be(HttpStatusCode.OK, moved.Raw);
        var newId = moved.Data.GetGuid();

        (await api.Admin.GetAsync($"/api/v1/students/{child}")).Data.GetProperty("status").GetString().Should().Be("transferred");
        (await api.Admin.PostAsync($"/api/v1/group/students/{child}/transfer", new { to_centre_id = id })).Status.Should().Be(HttpStatusCode.UnprocessableEntity);

        var branch = await BranchClientAsync(id);
        var copy = await branch.GetAsync($"/api/v1/students/{newId}");
        copy.Status.Should().Be(HttpStatusCode.OK, copy.Raw);
        copy.Data.GetProperty("status").GetString().Should().Be("active");
        copy.Data.GetProperty("medical_notes").GetString().Should().Be("Peanut allergy");
    }

    [Fact]
    public async Task A_teacher_can_be_given_a_second_centre_with_their_own_role_there()
    {
        var (id, slug) = await NewBranchAsync();
        var teacher = await api.CreateUserAsync("teacher");

        var assigned = await api.Admin.PostAsync("/api/v1/group/staff-assignments", new { email = teacher.Email, centre_id = id, role = "accounts" });
        assigned.Status.Should().Be(HttpStatusCode.NoContent, assigned.Raw);
        (await api.Admin.PostAsync("/api/v1/group/staff-assignments", new { email = teacher.Email, centre_id = id, role = "accounts" })).Status.Should().Be(HttpStatusCode.Conflict);
        (await api.Admin.PostAsync("/api/v1/group/staff-assignments", new { email = teacher.Email, centre_id = id, role = "parent" })).Status.Should().Be(HttpStatusCode.BadRequest);

        // Same password now matches two centres, so the person is asked which one; no password is typed again to switch.
        var both = await api.NewClient().PostAsync("/api/v1/auth/login", new { email = teacher.Email, password = teacher.Password });
        both.Data.GetProperty("choose_centre").GetArrayLength().Should().Be(2);

        var branch = await api.LoginAsync(teacher.Email, teacher.Password, slug);
        var me = await branch.GetAsync("/api/v1/auth/me");
        me.Data.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().Equal("accounts");
        (await teacher.Client.PostAsync("/api/v1/group/switch", new { centre_id = id })).Status.Should().Be(HttpStatusCode.OK);
    }
}
