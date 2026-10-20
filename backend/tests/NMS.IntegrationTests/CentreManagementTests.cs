using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CentreManagementTests(NmsApiFixture api)
{
    [Fact]
    public async Task Room_can_be_created_read_updated_and_deleted()
    {
        var name = $"Toddlers {Guid.NewGuid():N}";

        var created = await api.Admin.PostAsync("/api/v1/rooms", new
        {
            name = new { en = name, ar = "الأطفال" },
            capacity = 14,
            min_age_months = 18,
            max_age_months = 36,
            ratio_staff_to_student = 5,
        });
        var id = created.Data.GetProperty("id").GetGuid();
        var read = await api.Admin.GetAsync($"/api/v1/rooms/{id}");
        var updated = await api.Admin.PatchAsync($"/api/v1/rooms/{id}", new
        {
            name = new { en = name, ar = "الأطفال" },
            capacity = 16,
            min_age_months = 18,
            max_age_months = 36,
            ratio_staff_to_student = 5,
            is_active = true,
        });
        var deleted = await api.Admin.DeleteAsync($"/api/v1/rooms/{id}");
        var gone = await api.Admin.GetAsync($"/api/v1/rooms/{id}");

        created.Status.Should().Be(HttpStatusCode.Created);
        read.Data.GetProperty("name").GetProperty("ar").GetString().Should().Be("الأطفال");
        updated.Data.GetProperty("capacity").GetInt32().Should().Be(16);
        deleted.Status.Should().Be(HttpStatusCode.NoContent);
        gone.Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Duplicate_room_names_conflict_regardless_of_case()
    {
        var name = $"Babies {Guid.NewGuid():N}";
        await api.Admin.PostAsync("/api/v1/rooms", new { name = new { en = name }, capacity = 8, min_age_months = 0, max_age_months = 12, ratio_staff_to_student = 3 });

        var duplicate = await api.Admin.PostAsync("/api/v1/rooms", new { name = new { en = name.ToUpperInvariant() }, capacity = 8, min_age_months = 0, max_age_months = 12, ratio_staff_to_student = 3 });

        duplicate.Status.Should().Be(HttpStatusCode.Conflict);
        duplicate.ErrorCode.Should().Be("DUPLICATE_RESOURCE");
    }

    [Fact]
    public async Task Invalid_room_returns_field_level_validation_errors()
    {
        var reply = await api.Admin.PostAsync("/api/v1/rooms", new
        {
            name = new { ar = "بدون إنجليزي" },
            capacity = 0,
            min_age_months = 24,
            max_age_months = 12,
            ratio_staff_to_student = 4,
        });

        reply.Status.Should().Be(HttpStatusCode.BadRequest);
        var fields = reply.Root.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString()).ToList();
        fields.Should().Contain(["name", "capacity", "max_age_months"]);
    }

    [Fact]
    public async Task A_room_with_students_cannot_be_deleted()
    {
        var room = await api.CreateRoomAsync();
        await api.CreateStudentAsync(room);

        var delete = await api.Admin.DeleteAsync($"/api/v1/rooms/{room}");

        delete.Status.Should().Be((HttpStatusCode)422);
    }

    [Fact]
    public async Task Programs_groups_and_labels_support_create_update_and_delete()
    {
        var room = await api.CreateRoomAsync();

        var program = await api.Admin.PostAsync("/api/v1/programs", new
        {
            name = new { en = $"Full day {Guid.NewGuid():N}" },
            days_per_week = 5,
            start_time = "07:30:00",
            end_time = "14:30:00",
            age_group = "2-3 years",
        });
        var group = await api.Admin.PostAsync("/api/v1/groups", new { name = new { en = $"Stars {Guid.NewGuid():N}" }, room_id = room });
        var label = await api.Admin.PostAsync("/api/v1/labels", new { name = new { en = $"Allergy {Guid.NewGuid():N}" }, color = "#E07A5F" });

        var badLabel = await api.Admin.PostAsync("/api/v1/labels", new { name = new { en = "Bad" }, color = "red" });
        var badTimes = await api.Admin.PostAsync("/api/v1/programs", new
        {
            name = new { en = "Backwards" },
            days_per_week = 5,
            start_time = "15:00:00",
            end_time = "09:00:00",
            age_group = "x",
        });
        var groupInMissingRoom = await api.Admin.PostAsync("/api/v1/groups", new { name = new { en = "Ghost" }, room_id = Guid.NewGuid() });

        program.Status.Should().Be(HttpStatusCode.Created);
        group.Status.Should().Be(HttpStatusCode.Created);
        label.Status.Should().Be(HttpStatusCode.Created);
        badLabel.Status.Should().Be(HttpStatusCode.BadRequest);
        badTimes.Status.Should().Be(HttpStatusCode.BadRequest);
        groupInMissingRoom.Status.Should().Be(HttpStatusCode.BadRequest);

        var renamed = await api.Admin.PatchAsync($"/api/v1/labels/{label.Data.GetProperty("id").GetGuid()}", new { name = new { en = $"Renamed {Guid.NewGuid():N}" }, color = "#81B29A" });
        renamed.Data.GetProperty("color").GetString().Should().Be("#81B29A");
        (await api.Admin.DeleteAsync($"/api/v1/labels/{label.Data.GetProperty("id").GetGuid()}")).Status.Should().Be(HttpStatusCode.NoContent);
        (await api.Admin.DeleteAsync($"/api/v1/groups/{group.Data.GetProperty("id").GetGuid()}")).Status.Should().Be(HttpStatusCode.NoContent);
        (await api.Admin.DeleteAsync($"/api/v1/programs/{program.Data.GetProperty("id").GetGuid()}")).Status.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Student_is_created_with_a_unique_barcode_guardian_and_labels()
    {
        var room = await api.CreateRoomAsync();
        var label = (await api.Admin.PostAsync("/api/v1/labels", new { name = new { en = $"Vegetarian {Guid.NewGuid():N}" }, color = "#81B29A" })).Data.GetProperty("id").GetGuid();
        var email = $"mum-{Guid.NewGuid():N}@test.local";

        var created = await api.Admin.PostAsync("/api/v1/students", new
        {
            student = new
            {
                first_name = "Omar",
                last_name = "Saeed",
                first_name_arabic = "عمر",
                date_of_birth = "2022-11-03",
                gender = "male",
                room_id = room,
                label_ids = new[] { label },
                nationality = "AE",
            },
            guardians = new[] { new { first_name = "Fatima", last_name = "Saeed", email, relationship = "mother", is_primary = true } },
        });
        var id = created.Data.GetProperty("id").GetGuid();
        var guardians = await api.Admin.GetAsync($"/api/v1/students/{id}/guardians");

        created.Status.Should().Be(HttpStatusCode.Created, created.Raw);
        created.Data.GetProperty("barcode").GetString().Should().StartWith("STU");
        created.Data.GetProperty("status").GetString().Should().Be("signed_up");
        created.Data.GetProperty("label_ids").EnumerateArray().Select(l => l.GetGuid()).Should().Contain(label);
        guardians.Items.Should().ContainSingle().Which.GetProperty("email").GetString().Should().Be(email);
        created.Raw.Should().Contain("عمر");
    }

    [Fact]
    public async Task Siblings_share_one_guardian_record()
    {
        var email = $"shared-{Guid.NewGuid():N}@test.local";
        var first = await api.CreateStudentAsync(guardianEmail: email);
        var second = await api.CreateStudentAsync(guardianEmail: email);

        var a = (await api.Admin.GetAsync($"/api/v1/students/{first}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var b = (await api.Admin.GetAsync($"/api/v1/students/{second}/guardians")).Items.Single().GetProperty("id").GetGuid();

        a.Should().Be(b);
    }

    [Fact]
    public async Task Student_validation_and_missing_references_are_rejected()
    {
        var futureBirth = await api.Admin.PostAsync("/api/v1/students", new
        {
            student = new { first_name = "A", last_name = "B", date_of_birth = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd") },
        });
        var missingRoom = await api.Admin.PostAsync("/api/v1/students", new
        {
            student = new { first_name = "A", last_name = "B", date_of_birth = "2023-01-01", room_id = Guid.NewGuid() },
        });
        var badGuardian = await api.Admin.PostAsync("/api/v1/students", new
        {
            student = new { first_name = "A", last_name = "B", date_of_birth = "2023-01-01" },
            guardians = new[] { new { first_name = "G", last_name = "H", email = "nope", relationship = "mother" } },
        });

        futureBirth.Status.Should().Be(HttpStatusCode.BadRequest);
        missingRoom.Status.Should().Be(HttpStatusCode.BadRequest);
        missingRoom.Root.GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("room_id");
        badGuardian.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Student_status_follows_the_allowed_lifecycle()
    {
        var id = await api.CreateStudentAsync();

        var activate = await api.Admin.PostAsync($"/api/v1/students/{id}/actions/transition-status", new { status = "active" });
        var hold = await api.Admin.PostAsync($"/api/v1/students/{id}/actions/transition-status", new { status = "on_hold", reason = "Family travel" });
        var illegal = await api.Admin.PostAsync($"/api/v1/students/{id}/actions/transition-status", new { status = "signed_up" });
        var unknown = await api.Admin.PostAsync($"/api/v1/students/{id}/actions/transition-status", new { status = "graduated" });

        activate.Data.GetProperty("status").GetString().Should().Be("active");
        hold.Data.GetProperty("status_reason").GetString().Should().Be("Family travel");
        illegal.Status.Should().Be((HttpStatusCode)422);
        illegal.ErrorCode.Should().Be("INVALID_STATE_TRANSITION");
        unknown.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Student_can_be_updated_and_soft_deleted()
    {
        var id = await api.CreateStudentAsync(first: "Sara");

        var updated = await api.Admin.PatchAsync($"/api/v1/students/{id}", new
        {
            student = new { first_name = "Sarah", last_name = "Updated", date_of_birth = "2023-04-12", nationality = "JO" },
        });
        var deleted = await api.Admin.DeleteAsync($"/api/v1/students/{id}");
        var read = await api.Admin.GetAsync($"/api/v1/students/{id}");

        updated.Data.GetProperty("first_name").GetString().Should().Be("Sarah");
        deleted.Status.Should().Be(HttpStatusCode.NoContent);
        read.Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Guardians_can_be_added_updated_and_removed()
    {
        var student = await api.CreateStudentAsync();
        var email = $"dad-{Guid.NewGuid():N}@test.local";

        var added = await api.Admin.PostAsync($"/api/v1/students/{student}/guardians", new
        {
            first_name = "Khalid",
            last_name = "Ali",
            email,
            relationship = "father",
            is_primary = false,
        });
        var guardianId = added.Data.GetProperty("id").GetGuid();
        var duplicate = await api.Admin.PostAsync($"/api/v1/students/{student}/guardians", new { first_name = "K", last_name = "A", email, relationship = "father", is_primary = false });
        var updated = await api.Admin.PatchAsync($"/api/v1/students/{student}/guardians/{guardianId}", new
        {
            first_name = "Khaled",
            last_name = "Ali",
            email,
            relationship = "father",
            is_primary = true,
            can_pickup = false,
        });
        var removed = await api.Admin.DeleteAsync($"/api/v1/students/{student}/guardians/{guardianId}");
        var list = await api.Admin.GetAsync($"/api/v1/students/{student}/guardians");

        added.Status.Should().Be(HttpStatusCode.Created);
        duplicate.Status.Should().Be(HttpStatusCode.Conflict);
        updated.Data.GetProperty("can_pickup").GetBoolean().Should().BeFalse();
        removed.Status.Should().Be(HttpStatusCode.NoContent);
        list.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Lists_use_cursor_pagination_without_gaps_or_repeats()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var created = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            created.Add(await api.CreateStudentAsync(first: $"Page{tag}"));
        }

        var seen = new List<Guid>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = await api.Admin.GetAsync($"/api/v1/students?limit=2&search=Page{tag}" + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}"));
            seen.AddRange(page.Items.Select(i => i.GetProperty("id").GetGuid()));
            var pagination = page.Meta.GetProperty("pagination");
            cursor = pagination.GetProperty("next_cursor").GetString();
            pagination.GetProperty("limit").GetInt32().Should().Be(2);
            pages++;
        }
        while (cursor is not null && pages < 10);

        pages.Should().Be(3);
        seen.Should().HaveCount(5).And.OnlyHaveUniqueItems().And.BeEquivalentTo(created);
    }

    [Fact]
    public async Task Limit_is_capped_at_one_hundred()
    {
        var page = await api.Admin.GetAsync("/api/v1/students?limit=5000");

        page.Meta.GetProperty("pagination").GetProperty("limit").GetInt32().Should().Be(100);
    }

    [Fact]
    public async Task Staff_can_be_created_updated_and_deleted_with_a_unique_email()
    {
        var email = $"staff-{Guid.NewGuid():N}@test.local";
        var id = await api.CreateStaffAsync(email);

        var duplicate = await api.Admin.PostAsync("/api/v1/staff", new { staff = new { first_name = "A", last_name = "B", email, hire_date = "2025-01-01", status = "active" } });
        var read = await api.Admin.GetAsync($"/api/v1/staff/{id}");
        var updated = await api.Admin.PatchAsync($"/api/v1/staff/{id}", new
        {
            staff = new { first_name = "Noor", last_name = "Lead", email, hire_date = "2025-09-01", status = "on_leave", salary_amount_fils = 700000 },
        });
        var invalid = await api.Admin.PatchAsync($"/api/v1/staff/{id}", new
        {
            staff = new { first_name = "Noor", last_name = "Lead", email, hire_date = "2025-09-01", end_date = "2024-01-01", status = "active" },
        });
        var deleted = await api.Admin.DeleteAsync($"/api/v1/staff/{id}");

        duplicate.Status.Should().Be(HttpStatusCode.Conflict);
        read.Data.GetProperty("barcode").GetString().Should().StartWith("STF");
        read.Data.GetProperty("salary_amount_fils").GetInt64().Should().Be(650000);
        updated.Data.GetProperty("status").GetString().Should().Be("on_leave");
        invalid.Status.Should().Be(HttpStatusCode.BadRequest);
        deleted.Status.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Room_assignments_validate_times_and_rooms()
    {
        var staff = await api.CreateStaffAsync();
        var room = await api.CreateRoomAsync();

        await api.AssignStaffToRoomAsync(staff, room);
        var backwards = await api.Admin.PostAsync($"/api/v1/staff/{staff}/room-assignments", new
        {
            room_id = room,
            day_of_week = 2,
            start_time = "15:00:00",
            end_time = "08:00:00",
            effective_from = "2026-01-01",
        });
        var missingRoom = await api.Admin.PostAsync($"/api/v1/staff/{staff}/room-assignments", new
        {
            room_id = Guid.NewGuid(),
            day_of_week = 2,
            start_time = "08:00:00",
            end_time = "15:00:00",
            effective_from = "2026-01-01",
        });
        var list = await api.Admin.GetAsync($"/api/v1/staff/{staff}/room-assignments");

        backwards.Status.Should().Be(HttpStatusCode.BadRequest);
        missingRoom.Status.Should().Be(HttpStatusCode.BadRequest);
        list.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Unknown_ids_return_not_found_in_the_standard_envelope()
    {
        var reply = await api.Admin.GetAsync($"/api/v1/students/{Guid.NewGuid()}");

        reply.Status.Should().Be(HttpStatusCode.NotFound);
        reply.Root.GetProperty("success").GetBoolean().Should().BeFalse();
        reply.ErrorCode.Should().Be("RESOURCE_NOT_FOUND");
    }
}
