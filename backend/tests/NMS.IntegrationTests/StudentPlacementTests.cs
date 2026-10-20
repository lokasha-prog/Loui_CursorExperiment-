using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class StudentPlacementTests(NmsApiFixture api)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 1, 2, 3];

    private static string Born(int monthsAgo) => DateTime.UtcNow.AddMonths(-monthsAgo).AddDays(-3).ToString("yyyy-MM-dd");

    private async Task<Guid> ProgramAsync(int min, int max, string name = "Prog")
    {
        var reply = await api.Admin.PostAsync("/api/v1/programs", new
        {
            name = new { en = $"{name} {Guid.NewGuid():N}" }, days_per_week = 5, start_time = "07:30:00", end_time = "14:00:00",
            age_group = "x", min_age_months = min, max_age_months = max,
        });
        reply.Status.Should().Be(HttpStatusCode.Created, reply.Raw);
        return reply.Data.GetProperty("id").GetGuid();
    }

    private Task<ApiReply> CreateAsync(object student) =>
        api.Admin.PostAsync("/api/v1/students", new { student, guardians = Array.Empty<object>() });

    [Fact]
    public async Task A_child_can_only_be_placed_in_a_room_and_programme_that_fit_their_age()
    {
        var toddlers = await api.CreateRoomAsync();
        var fits = await ProgramAsync(12, 36);
        var tooOld = await ProgramAsync(48, 72);

        var ok = await CreateAsync(new { first_name = "A", last_name = "B", date_of_birth = Born(24), room_id = toddlers, program_id = fits });
        var wrongRoom = await CreateAsync(new { first_name = "A", last_name = "B", date_of_birth = Born(60), room_id = toddlers });
        var wrongProgram = await CreateAsync(new { first_name = "A", last_name = "B", date_of_birth = Born(24), program_id = tooOld });

        ok.Status.Should().Be(HttpStatusCode.Created, ok.Raw);
        wrongRoom.Status.Should().Be(HttpStatusCode.BadRequest);
        wrongRoom.Raw.Should().Contain("room_id");
        wrongProgram.Status.Should().Be(HttpStatusCode.BadRequest);
        wrongProgram.Raw.Should().Contain("program_id");
    }

    [Fact]
    public async Task Editing_other_details_of_a_child_who_has_aged_out_of_their_room_is_still_allowed()
    {
        var room = await api.CreateRoomAsync();
        var id = await api.CreateStudentAsync(room);
        var barcode = (await api.Admin.GetAsync($"/api/v1/students/{id}")).Data;
        await api.WithTenantAsync(api.TenantId, async db =>
        {
            var student = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(db.Students, s => s.Id == id);
            student.DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-80));
            await db.SaveChangesAsync();
            return 0;
        });

        var edit = await api.Admin.PatchAsync($"/api/v1/students/{id}", new
        {
            student = new { first_name = "Renamed", last_name = "Child", date_of_birth = DateTime.UtcNow.AddMonths(-80).ToString("yyyy-MM-dd"), room_id = room, first_name_arabic = "اسم جديد" },
        });

        edit.Status.Should().Be(HttpStatusCode.OK, edit.Raw);
        edit.Data.GetProperty("first_name_arabic").GetString().Should().Be("اسم جديد");
        barcode.GetProperty("id").GetGuid().Should().Be(id);
    }

    [Fact]
    public async Task Gender_is_male_or_female_and_nationality_must_be_a_country_code()
    {
        var other = await CreateAsync(new { first_name = "A", last_name = "B", date_of_birth = Born(24), gender = "other" });
        var unspecified = await CreateAsync(new { first_name = "A", last_name = "B", date_of_birth = Born(24), gender = "unspecified" });
        var free = await CreateAsync(new { first_name = "A", last_name = "B", date_of_birth = Born(24), nationality = "Emirati" });
        var good = await CreateAsync(new { first_name = "A", last_name = "B", date_of_birth = Born(24), gender = "female", nationality = "AE" });

        other.Status.Should().Be(HttpStatusCode.BadRequest);
        unspecified.Status.Should().Be(HttpStatusCode.BadRequest);
        free.Status.Should().Be(HttpStatusCode.BadRequest);
        good.Status.Should().Be(HttpStatusCode.Created, good.Raw);
    }

    [Fact]
    public async Task Identity_numbers_and_photos_are_stored_for_child_and_guardian_and_hidden_from_families()
    {
        var room = await api.CreateRoomAsync();
        var photo = (await api.Admin.PostFileAsync("/api/v1/media", Png, "child.png")).Data.GetProperty("id").GetGuid();
        var idPhoto = (await api.Admin.PostFileAsync("/api/v1/media", Png, "id.png")).Data.GetProperty("id").GetGuid();
        var email = $"mum-{Guid.NewGuid():N}@test.local";
        var created = await api.Admin.PostAsync("/api/v1/students", new
        {
            student = new { first_name = "Layla", last_name = "Id", date_of_birth = Born(24), room_id = room, emirates_id = "784-2022-1234567-1", photo_media_id = photo, id_photo_media_id = idPhoto },
            guardians = new[] { new { first_name = "Mum", last_name = "Id", email, relationship = "mother", is_primary = true, emirates_id = "784-1990-7654321-2" } },
        });
        var id = created.Data.GetProperty("id").GetGuid();
        var guardianId = (await api.Admin.GetAsync($"/api/v1/students/{id}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardianId);
        var teacherStaff = await api.CreateStaffAsync();
        await api.AssignStaffToRoomAsync(teacherStaff, room);
        var teacher = await api.CreateUserAsync("teacher", staffId: teacherStaff);

        var staffView = await api.Admin.GetAsync($"/api/v1/students/{id}");
        var staffGuardians = await api.Admin.GetAsync($"/api/v1/students/{id}/guardians");
        var parentView = await parent.Client.GetAsync($"/api/v1/students/{id}");
        var parentGuardians = await parent.Client.GetAsync($"/api/v1/students/{id}/guardians");

        created.Status.Should().Be(HttpStatusCode.Created, created.Raw);
        staffView.Data.GetProperty("emirates_id").GetString().Should().Be("784-2022-1234567-1");
        staffView.Data.GetProperty("id_photo_media_id").GetGuid().Should().Be(idPhoto);
        staffGuardians.Items.Single().GetProperty("emirates_id").GetString().Should().Be("784-1990-7654321-2");
        parentView.Data.GetProperty("emirates_id").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        parentView.Data.GetProperty("id_photo_media_id").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        parentView.Data.GetProperty("photo_media_id").GetGuid().Should().Be(photo);
        parentGuardians.Items.Single().GetProperty("emirates_id").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

        (await parent.Client.GetBytesAsync($"/api/v1/media/{photo}/content")).Status.Should().Be(HttpStatusCode.OK, "a family can see their child's profile photo");
        (await parent.Client.GetBytesAsync($"/api/v1/media/{idPhoto}/content")).Status.Should().Be(HttpStatusCode.NotFound);
        (await teacher.Client.GetBytesAsync($"/api/v1/media/{idPhoto}/content")).Status.Should().Be(HttpStatusCode.NotFound, "only staff who manage students see identity documents");
        (await api.Admin.GetBytesAsync($"/api/v1/media/{idPhoto}/content")).Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_missing_or_non_image_file_cannot_be_used_as_a_photo()
    {
        var reply = await CreateAsync(new { first_name = "A", last_name = "B", date_of_birth = Born(24), photo_media_id = Guid.NewGuid() });

        reply.Status.Should().Be(HttpStatusCode.BadRequest);
    }
}
