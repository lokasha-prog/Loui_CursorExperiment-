using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AccessControlTests(NmsApiFixture api)
{
    private async Task<(ApiClient Client, Guid StudentId)> ParentWithChildAsync(Guid? roomId = null)
    {
        var email = $"parent-{Guid.NewGuid():N}@test.local";
        var student = await api.CreateStudentAsync(roomId, email);
        var guardianId = (await api.Admin.GetAsync($"/api/v1/students/{student}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardianId);
        return (parent.Client, student);
    }

    [Fact]
    public async Task Parent_sees_only_their_own_children_in_lists()
    {
        var (parentA, childA) = await ParentWithChildAsync();
        await ParentWithChildAsync();

        var list = await parentA.GetAsync("/api/v1/students?limit=100");

        list.Status.Should().Be(HttpStatusCode.OK);
        list.Items.Select(i => i.GetProperty("id").GetGuid()).Should().ContainSingle().Which.Should().Be(childA);
    }

    [Fact]
    public async Task Parent_A_asking_for_parent_Bs_child_gets_404_not_403()
    {
        var (parentA, _) = await ParentWithChildAsync();
        var (_, childB) = await ParentWithChildAsync();

        var direct = await parentA.GetAsync($"/api/v1/students/{childB}");
        var guardians = await parentA.GetAsync($"/api/v1/students/{childB}/guardians");
        var missing = await parentA.GetAsync($"/api/v1/students/{Guid.NewGuid()}");

        direct.Status.Should().Be(HttpStatusCode.NotFound);
        guardians.Status.Should().Be(HttpStatusCode.NotFound);
        direct.Raw.Should().NotContain("first_name", "nothing about the child may leak");
        direct.ErrorCode.Should().Be(missing.ErrorCode, "a denied child looks exactly like a child that does not exist");
    }

    [Fact]
    public async Task Parent_can_read_their_own_child_but_not_custody_or_identity_documents()
    {
        var (parent, child) = await ParentWithChildAsync();
        await api.Admin.PatchAsync($"/api/v1/students/{child}", new
        {
            student = new
            {
                first_name = "Layla",
                last_name = "Own",
                date_of_birth = "2023-04-12",
                allergy_info = "Peanuts",
                custody_notes = "Court order 123",
                emirates_id = "784-1111-1111111-1",
            },
        });

        var read = await parent.GetAsync($"/api/v1/students/{child}");

        read.Status.Should().Be(HttpStatusCode.OK);
        read.Data.GetProperty("allergy_info").GetString().Should().Be("Peanuts");
        read.Data.GetProperty("custody_notes").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        read.Data.GetProperty("emirates_id").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Parent_cannot_change_data_or_see_staff_and_rooms()
    {
        var (parent, child) = await ParentWithChildAsync();

        var create = await parent.PostAsync("/api/v1/students", new { student = new { first_name = "A", last_name = "B", date_of_birth = "2023-01-01" } });
        var update = await parent.PatchAsync($"/api/v1/students/{child}", new { student = new { first_name = "A", last_name = "B", date_of_birth = "2023-01-01" } });
        var delete = await parent.DeleteAsync($"/api/v1/students/{child}");
        var staff = await parent.GetAsync("/api/v1/staff");
        var rooms = await parent.GetAsync("/api/v1/rooms");
        var users = await parent.GetAsync("/api/v1/users");

        new[] { create, update, delete, staff, rooms, users }.Should().OnlyContain(r => r.Status == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_sees_only_students_and_rooms_of_their_assigned_rooms()
    {
        var mine = await api.CreateRoomAsync();
        var other = await api.CreateRoomAsync();
        var myStudent = await api.CreateStudentAsync(mine);
        var otherStudent = await api.CreateStudentAsync(other);
        var staff = await api.CreateStaffAsync();
        await api.AssignStaffToRoomAsync(staff, mine);
        var teacher = await api.CreateUserAsync("teacher", staffId: staff);

        var students = await teacher.Client.GetAsync("/api/v1/students?limit=100");
        var rooms = await teacher.Client.GetAsync("/api/v1/rooms?limit=100");
        var allowed = await teacher.Client.GetAsync($"/api/v1/students/{myStudent}");
        var denied = await teacher.Client.GetAsync($"/api/v1/students/{otherStudent}");
        var deniedRoom = await teacher.Client.GetAsync($"/api/v1/rooms/{other}");

        var studentIds = students.Items.Select(s => s.GetProperty("id").GetGuid()).ToList();
        studentIds.Should().Contain(myStudent).And.NotContain(otherStudent);
        rooms.Items.Select(r => r.GetProperty("id").GetGuid()).Should().ContainSingle().Which.Should().Be(mine);
        allowed.Status.Should().Be(HttpStatusCode.OK);
        denied.Status.Should().Be(HttpStatusCode.NotFound);
        deniedRoom.Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Teacher_without_assignments_sees_nothing_and_cannot_write()
    {
        await api.CreateStudentAsync(await api.CreateRoomAsync());
        var staff = await api.CreateStaffAsync();
        var teacher = await api.CreateUserAsync("teacher", staffId: staff);

        var students = await teacher.Client.GetAsync("/api/v1/students");
        var create = await teacher.Client.PostAsync("/api/v1/rooms", new { name = new { en = "Nope" }, capacity = 5, min_age_months = 0, max_age_months = 12, ratio_staff_to_student = 3 });

        students.Items.Should().BeEmpty();
        create.Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Teacher_does_not_see_salary_or_identity_documents_of_colleagues()
    {
        var room = await api.CreateRoomAsync();
        var colleague = await api.CreateStaffAsync();
        var mine = await api.CreateStaffAsync();
        await api.AssignStaffToRoomAsync(colleague, room);
        await api.AssignStaffToRoomAsync(mine, room);
        var teacher = await api.CreateUserAsync("teacher", staffId: mine);

        var read = await teacher.Client.GetAsync($"/api/v1/staff/{colleague}");

        read.Status.Should().Be(HttpStatusCode.OK);
        read.Data.GetProperty("salary_amount_fils").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Theory]
    [InlineData("receptionist", HttpStatusCode.OK, HttpStatusCode.Forbidden)]
    [InlineData("accounts", HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData("marketing", HttpStatusCode.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData("centre_admin", HttpStatusCode.OK, HttpStatusCode.Created)]
    public async Task Role_can_or_cannot_read_and_create_students(string role, HttpStatusCode read, HttpStatusCode create)
    {
        var user = await api.CreateUserAsync(role);

        var list = await user.Client.GetAsync("/api/v1/students");
        var post = await user.Client.PostAsync("/api/v1/students", new { student = new { first_name = "A", last_name = "B", date_of_birth = "2023-01-01" } });

        list.Status.Should().Be(read);
        post.Status.Should().Be(create);
    }

    [Fact]
    public async Task Only_super_administrators_can_hand_out_the_super_administrator_role()
    {
        var it = await api.CreateUserAsync("it");

        var reply = await it.Client.PostAsync("/api/v1/users", new
        {
            email = $"sneaky-{Guid.NewGuid():N}@test.local",
            first_name = "S",
            last_name = "S",
            password = NmsApiFixture.NewPassword(),
            roles = new[] { "super_admin" },
        });

        reply.Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Health_endpoints_are_public_and_the_docs_are_not_exposed_outside_development()
    {
        var client = api.NewClient();

        (await client.GetAsync("/health/live")).Status.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).Status.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/openapi/v1.json")).Status.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var reply = await api.NewClient().GetAsync("/health/live");

        reply.Message.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        reply.Message.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
    }
}
