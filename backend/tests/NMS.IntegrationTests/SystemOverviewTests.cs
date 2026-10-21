using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SystemOverviewTests(NmsApiFixture api)
{
    [Fact]
    public async Task A_manager_sees_every_part_of_the_system_and_the_learning_numbers_move_with_the_work()
    {
        var before = await api.Admin.GetAsync("/api/v1/dashboard/system");
        var room = await api.CreateRoomAsync();
        var child = await api.CreateStudentAsync(room);
        await api.Admin.PostAsync($"/api/v1/students/{child}/actions/transition-status", new { status = "active" });
        await api.Admin.PostAsync("/api/v1/learning/observations", new { student_id = child, type = "concern", notes = new { en = "Look closer" } });
        var after = await api.Admin.GetAsync("/api/v1/dashboard/system");

        after.Status.Should().Be(HttpStatusCode.OK, after.Raw);
        foreach (var block in new[] { "people", "billing", "admissions", "learning", "communication" })
        {
            after.Data.GetProperty(block).ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object, block);
        }

        after.Data.GetProperty("family").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        after.Data.GetProperty("learning").GetProperty("concerns_last30_days").GetInt32()
            .Should().Be(before.Data.GetProperty("learning").GetProperty("concerns_last30_days").GetInt32() + 1);
    }

    [Fact]
    public async Task Each_role_sees_only_its_own_blocks_and_a_family_sees_only_its_own_children()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var child = await api.CreateStudentAsync(await api.CreateRoomAsync(), email);
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = (await api.CreateUserAsync("parent", guardianId: guardian)).Client;

        var asTeacher = await teacher.Client.GetAsync("/api/v1/dashboard/system");
        var asParent = await parent.GetAsync("/api/v1/dashboard/system");

        asTeacher.Data.GetProperty("billing").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        asTeacher.Data.GetProperty("admissions").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        asTeacher.Data.GetProperty("learning").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
        foreach (var block in new[] { "people", "billing", "admissions", "learning", "communication" })
        {
            asParent.Data.GetProperty(block).ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null, block);
        }

        asParent.Data.GetProperty("family").GetProperty("children").GetInt32().Should().Be(1);
    }
}
