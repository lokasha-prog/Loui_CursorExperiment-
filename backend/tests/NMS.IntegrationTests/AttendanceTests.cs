using System.Net;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AttendanceTests(NmsApiFixture api)
{
    private sealed record Child(Guid Id, string Barcode, Guid GuardianId);

    private async Task<Child> ActiveChildAsync(Guid? roomId = null)
    {
        var id = await api.CreateStudentAsync(roomId, $"guardian-{Guid.NewGuid():N}@test.local");
        (await api.Admin.PostAsync($"/api/v1/students/{id}/actions/transition-status", new { status = "active" }))
            .Status.Should().Be(HttpStatusCode.OK);
        var detail = await api.Admin.GetAsync($"/api/v1/students/{id}");
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{id}/guardians")).Items.Single().GetProperty("id").GetGuid();
        return new Child(id, detail.Data.GetProperty("barcode").GetString()!, guardian);
    }

    private async Task<string> StaffBarcodeAsync(Guid staffId) =>
        (await api.Admin.GetAsync($"/api/v1/staff/{staffId}")).Data.GetProperty("barcode").GetString()!;

    private Task<ApiReply> CheckInAsync(ApiClient client, string barcode) =>
        client.PostAsync("/api/v1/attendance/actions/check-in", new { barcode });

    private async Task<int> EventsAsync(string eventType, string? barcode = null) =>
        await api.WithTenantAsync(api.TenantId, db => db.AttendanceEvents
            .CountAsync(e => e.EventType == eventType && (barcode == null || e.BarcodeScanned == barcode)));

    [Fact]
    public async Task Scanning_a_child_in_records_them_present_and_a_second_scan_is_refused_but_logged()
    {
        var room = await api.CreateRoomAsync();
        var child = await ActiveChildAsync(room);

        var first = await CheckInAsync(api.Admin, child.Barcode);
        var second = await CheckInAsync(api.Admin, child.Barcode);

        first.Status.Should().Be(HttpStatusCode.OK, first.Raw);
        first.Data.GetProperty("status").GetString().Should().BeOneOf("present", "late");
        first.Data.GetProperty("check_in_method").GetString().Should().Be("barcode");
        first.Data.GetProperty("room_id").GetGuid().Should().Be(room);
        second.Status.Should().Be(HttpStatusCode.Conflict);
        (await EventsAsync("already_checked_in", child.Barcode)).Should().Be(1);
        (await EventsAsync("check_in", child.Barcode)).Should().Be(1);
    }

    [Fact]
    public async Task An_unknown_barcode_is_not_found_and_the_failed_scan_is_recorded()
    {
        var code = $"NOPE{Guid.NewGuid():N}"[..20];

        var reply = await CheckInAsync(api.Admin, code);

        reply.Status.Should().Be(HttpStatusCode.NotFound);
        (await EventsAsync("failed_lookup", code)).Should().Be(1);
    }

    [Fact]
    public async Task A_child_who_has_not_started_yet_cannot_be_checked_in()
    {
        var id = await api.CreateStudentAsync(await api.CreateRoomAsync(), null);
        var barcode = (await api.Admin.GetAsync($"/api/v1/students/{id}")).Data.GetProperty("barcode").GetString()!;

        var reply = await CheckInAsync(api.Admin, barcode);

        reply.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Manual_check_in_works_without_a_barcode()
    {
        var child = await ActiveChildAsync(await api.CreateRoomAsync());

        var reply = await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { person_type = "student", person_id = child.Id });

        reply.Status.Should().Be(HttpStatusCode.OK, reply.Raw);
        reply.Data.GetProperty("check_in_method").GetString().Should().Be("manual");
    }

    [Fact]
    public async Task Check_out_needs_an_authorised_guardian_and_a_confirmed_identity()
    {
        var child = await ActiveChildAsync(await api.CreateRoomAsync());
        await CheckInAsync(api.Admin, child.Barcode);

        var noGuardian = await api.Admin.PostAsync("/api/v1/attendance/actions/check-out", new { barcode = child.Barcode });
        var unverified = await api.Admin.PostAsync("/api/v1/attendance/actions/check-out", new { barcode = child.Barcode, guardian_id = child.GuardianId, pickup_identity_verified = false });
        var stranger = await api.Admin.PostAsync("/api/v1/attendance/actions/check-out", new { barcode = child.Barcode, guardian_id = Guid.NewGuid(), pickup_identity_verified = true });
        var ok = await api.Admin.PostAsync("/api/v1/attendance/actions/check-out", new { barcode = child.Barcode, guardian_id = child.GuardianId, pickup_identity_verified = true });
        var again = await api.Admin.PostAsync("/api/v1/attendance/actions/check-out", new { barcode = child.Barcode, guardian_id = child.GuardianId, pickup_identity_verified = true });

        noGuardian.Status.Should().Be(HttpStatusCode.BadRequest);
        unverified.Status.Should().Be(HttpStatusCode.BadRequest);
        stranger.Status.Should().Be(HttpStatusCode.BadRequest);
        ok.Status.Should().Be(HttpStatusCode.OK, ok.Raw);
        ok.Data.GetProperty("check_out_guardian_id").GetGuid().Should().Be(child.GuardianId);
        again.Status.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Checking_out_someone_who_never_arrived_is_refused()
    {
        var child = await ActiveChildAsync(await api.CreateRoomAsync());

        var reply = await api.Admin.PostAsync("/api/v1/attendance/actions/check-out", new { barcode = child.Barcode, guardian_id = child.GuardianId, pickup_identity_verified = true });

        reply.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_child_marked_absent_can_still_arrive_later_and_becomes_present()
    {
        var child = await ActiveChildAsync(await api.CreateRoomAsync());

        var absent = await api.Admin.PostAsync("/api/v1/attendance/actions/mark-absent", new { student_id = child.Id, notes = "Sick" });
        var arrived = await CheckInAsync(api.Admin, child.Barcode);
        var afterwards = await api.Admin.PostAsync("/api/v1/attendance/actions/mark-absent", new { student_id = child.Id });

        absent.Data.GetProperty("status").GetString().Should().Be("absent");
        arrived.Status.Should().Be(HttpStatusCode.OK, arrived.Raw);
        arrived.Data.GetProperty("status").GetString().Should().BeOneOf("present", "late");
        afterwards.Status.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task The_attendance_list_shows_names_and_filters_by_room()
    {
        var room = await api.CreateRoomAsync();
        var child = await ActiveChildAsync(room);
        await CheckInAsync(api.Admin, child.Barcode);

        var list = await api.Admin.GetAsync($"/api/v1/attendance?room_id={room}&person_type=student");

        list.Status.Should().Be(HttpStatusCode.OK);
        var row = list.Items.Should().ContainSingle().Subject;
        row.GetProperty("person_id").GetGuid().Should().Be(child.Id);
        row.GetProperty("person_name").GetString().Should().Contain("Layla");
    }

    [Fact]
    public async Task Live_ratio_flags_a_room_with_too_few_staff_and_clears_when_help_arrives()
    {
        var room = await api.CreateRoomAsync(capacity: 12, ratio: 4);
        for (var i = 0; i < 5; i++)
        {
            (await CheckInAsync(api.Admin, (await ActiveChildAsync(room)).Barcode)).Status.Should().Be(HttpStatusCode.OK);
        }

        var firstStaff = await api.CreateStaffAsync();
        var checkInStaff = await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode = await StaffBarcodeAsync(firstStaff), room_id = room });
        var short1 = (await api.Admin.GetAsync("/api/v1/attendance/ratio/live")).Items.Single(r => r.GetProperty("room_id").GetGuid() == room);

        var secondStaff = await api.CreateStaffAsync();
        await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode = await StaffBarcodeAsync(secondStaff), room_id = room });
        var enough = (await api.Admin.GetAsync("/api/v1/attendance/ratio/live")).Items.Single(r => r.GetProperty("room_id").GetGuid() == room);

        checkInStaff.Status.Should().Be(HttpStatusCode.OK, checkInStaff.Raw);
        short1.GetProperty("students_present").GetInt32().Should().Be(5);
        short1.GetProperty("staff_required").GetInt32().Should().Be(2);
        short1.GetProperty("is_breach").GetBoolean().Should().BeTrue();
        enough.GetProperty("staff_present").GetInt32().Should().Be(2);
        enough.GetProperty("is_breach").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task A_teacher_works_only_in_their_own_rooms()
    {
        var myRoom = await api.CreateRoomAsync();
        var otherRoom = await api.CreateRoomAsync();
        var mine = await ActiveChildAsync(myRoom);
        var theirs = await ActiveChildAsync(otherRoom);
        var staff = await api.CreateStaffAsync();
        await api.AssignStaffToRoomAsync(staff, myRoom);
        var teacher = await api.CreateUserAsync("teacher", staffId: staff);

        var allowed = await CheckInAsync(teacher.Client, mine.Barcode);
        var denied = await CheckInAsync(teacher.Client, theirs.Barcode);
        var list = await teacher.Client.GetAsync("/api/v1/attendance");

        allowed.Status.Should().Be(HttpStatusCode.OK, allowed.Raw);
        denied.Status.Should().Be(HttpStatusCode.NotFound);
        list.Items.Select(i => i.GetProperty("room_id").GetGuid()).Should().OnlyContain(r => r == myRoom);
    }

    [Fact]
    public async Task Parents_can_see_their_own_childs_attendance_but_cannot_record_any()
    {
        var email = $"parent-{Guid.NewGuid():N}@test.local";
        var childId = await api.CreateStudentAsync(await api.CreateRoomAsync(), email);
        await api.Admin.PostAsync($"/api/v1/students/{childId}/actions/transition-status", new { status = "active" });
        var barcode = (await api.Admin.GetAsync($"/api/v1/students/{childId}")).Data.GetProperty("barcode").GetString()!;
        var guardianId = (await api.Admin.GetAsync($"/api/v1/students/{childId}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardianId);
        await CheckInAsync(api.Admin, barcode);
        await ActiveChildAsync(await api.CreateRoomAsync());

        var scan = await CheckInAsync(parent.Client, barcode);
        var list = await parent.Client.GetAsync("/api/v1/attendance");

        scan.Status.Should().Be(HttpStatusCode.Forbidden);
        list.Items.Select(i => i.GetProperty("person_id").GetGuid()).Should().ContainSingle().Which.Should().Be(childId);
    }

    [Fact]
    public async Task The_scan_log_can_never_be_changed_or_removed()
    {
        var child = await ActiveChildAsync(await api.CreateRoomAsync());
        await CheckInAsync(api.Admin, child.Barcode);

        var update = () => RawAsync("UPDATE AttendanceEvents SET Outcome = 'error'");
        var delete = () => RawAsync("DELETE FROM AttendanceEvents");

        (await update.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("append-only");
        (await delete.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("append-only");
    }

    [Fact]
    public async Task Another_centre_cannot_see_these_attendance_rows()
    {
        var child = await ActiveChildAsync(await api.CreateRoomAsync());
        await CheckInAsync(api.Admin, child.Barcode);

        var asNobody = await api.WithTenantAsync(Guid.NewGuid(), db => db.AttendanceRecords.CountAsync());

        asNobody.Should().Be(0);
    }

    [Fact]
    public async Task Dashboard_summary_counts_the_day_for_the_centre()
    {
        var room = await api.CreateRoomAsync();
        var arrived = await ActiveChildAsync(room);
        var absent = await ActiveChildAsync(room);
        await ActiveChildAsync(room);
        await CheckInAsync(api.Admin, arrived.Barcode);
        await api.Admin.PostAsync("/api/v1/attendance/actions/mark-absent", new { student_id = absent.Id });

        var summary = await api.Admin.GetAsync("/api/v1/dashboard/attendance-summary");

        summary.Status.Should().Be(HttpStatusCode.OK);
        summary.Data.GetProperty("students_present").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        summary.Data.GetProperty("students_absent").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        summary.Data.GetProperty("active_students").GetInt32().Should().BeGreaterThanOrEqualTo(3);
        summary.Data.GetProperty("students_not_yet_arrived").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }

    private async Task RawAsync(string sql)
    {
        await using var connection = new SqlConnection(api.DatabaseConnection);
        await connection.OpenAsync();
        await using var context = connection.CreateCommand();
        context.CommandText = "EXEC sp_set_session_context @key = N'tenant_id', @value = @t;";
        context.Parameters.AddWithValue("@t", api.TenantId);
        await context.ExecuteNonQueryAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}

[Collection(ApiCollection.Name)]
public class LeaveTests(NmsApiFixture api)
{
    private async Task<Guid> LeaveTypeAsync(string name, bool requiresApproval = true, string appliesTo = "both")
    {
        var response = await api.Admin.PostAsync("/api/v1/leave-types", new
        {
            name = new Dictionary<string, string> { ["en"] = $"{name} {Guid.NewGuid():N}" },
            applies_to = appliesTo,
            requires_approval = requiresApproval,
        });
        response.Status.Should().Be(HttpStatusCode.Created, response.Raw);
        return response.Data.GetProperty("id").GetGuid();
    }

    private async Task<Guid> ChildAsync() => await api.CreateStudentAsync(await api.CreateRoomAsync());

    private static string Day(int offset) => DateTime.UtcNow.AddDays(offset).ToString("yyyy-MM-dd");

    [Fact]
    public async Task Leave_types_are_managed_by_admins_and_names_must_be_unique()
    {
        var name = $"Sick {Guid.NewGuid():N}";
        var payload = new { name = new Dictionary<string, string> { ["en"] = name }, applies_to = "student" };
        var teacher = await api.CreateUserAsync("teacher");

        var created = await api.Admin.PostAsync("/api/v1/leave-types", payload);
        var duplicate = await api.Admin.PostAsync("/api/v1/leave-types", payload);
        var forbidden = await teacher.Client.PostAsync("/api/v1/leave-types", payload);
        var list = await teacher.Client.GetAsync("/api/v1/leave-types");

        created.Status.Should().Be(HttpStatusCode.Created);
        duplicate.Status.Should().Be(HttpStatusCode.Conflict);
        forbidden.Status.Should().Be(HttpStatusCode.Forbidden);
        list.Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_leave_request_goes_through_submit_approve_and_cannot_be_decided_twice()
    {
        var type = await LeaveTypeAsync("Family");
        var child = await ChildAsync();

        var created = await api.Admin.PostAsync("/api/v1/leaves", new
        {
            person_type = "student", person_id = child, leave_type_id = type, start_date = Day(1), end_date = Day(3), reason = "Travel",
        });
        var id = created.Data.GetProperty("id").GetGuid();
        var approved = await api.Admin.PostAsync($"/api/v1/leaves/{id}/actions/approve", new { notes = "OK" });
        var again = await api.Admin.PostAsync($"/api/v1/leaves/{id}/actions/reject", new { });

        created.Status.Should().Be(HttpStatusCode.Created, created.Raw);
        created.Data.GetProperty("status").GetString().Should().Be("submitted");
        approved.Data.GetProperty("status").GetString().Should().Be("approved");
        approved.Data.GetProperty("approval_notes").GetString().Should().Be("OK");
        again.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Types_that_need_no_approval_are_approved_immediately()
    {
        var type = await LeaveTypeAsync("Holiday", requiresApproval: false);

        var reply = await api.Admin.PostAsync("/api/v1/leaves", new
        {
            person_type = "student", person_id = await ChildAsync(), leave_type_id = type, start_date = Day(1), end_date = Day(1),
        });

        reply.Data.GetProperty("status").GetString().Should().Be("approved");
    }

    [Fact]
    public async Task Overlapping_dates_and_mismatched_types_are_refused_but_a_rejected_request_frees_the_dates()
    {
        var both = await LeaveTypeAsync("Any");
        var staffOnly = await LeaveTypeAsync("StaffOnly", appliesTo: "staff");
        var child = await ChildAsync();
        object Body(Guid type, string from, string to) => new { person_type = "student", person_id = child, leave_type_id = type, start_date = from, end_date = to };

        var first = await api.Admin.PostAsync("/api/v1/leaves", Body(both, Day(5), Day(7)));
        var overlap = await api.Admin.PostAsync("/api/v1/leaves", Body(both, Day(7), Day(9)));
        var wrongKind = await api.Admin.PostAsync("/api/v1/leaves", Body(staffOnly, Day(20), Day(21)));
        var backwards = await api.Admin.PostAsync("/api/v1/leaves", Body(both, Day(12), Day(10)));
        await api.Admin.PostAsync($"/api/v1/leaves/{first.Data.GetProperty("id").GetGuid()}/actions/reject", new { });
        var retry = await api.Admin.PostAsync("/api/v1/leaves", Body(both, Day(7), Day(9)));

        overlap.Status.Should().Be(HttpStatusCode.Conflict);
        wrongKind.Status.Should().Be(HttpStatusCode.BadRequest);
        backwards.Status.Should().Be(HttpStatusCode.BadRequest);
        retry.Status.Should().Be(HttpStatusCode.Created, retry.Raw);
    }

    [Fact]
    public async Task Only_managers_decide_but_staff_can_cancel_and_lists_filter_by_status()
    {
        var type = await LeaveTypeAsync("Appointment");
        var teacherRoom = await api.CreateRoomAsync();
        var staff = await api.CreateStaffAsync();
        await api.AssignStaffToRoomAsync(staff, teacherRoom);
        var teacher = await api.CreateUserAsync("teacher", staffId: staff);
        var child = await api.CreateStudentAsync(teacherRoom);
        var created = await teacher.Client.PostAsync("/api/v1/leaves", new
        {
            person_type = "student", person_id = child, leave_type_id = type, start_date = Day(2), end_date = Day(2),
        });
        var id = created.Data.GetProperty("id").GetGuid();

        var denied = await teacher.Client.PostAsync($"/api/v1/leaves/{id}/actions/approve", new { });
        var cancelled = await teacher.Client.PostAsync($"/api/v1/leaves/{id}/actions/cancel");
        var submitted = await api.Admin.GetAsync("/api/v1/leaves?status=submitted&limit=100");
        var cancelledList = await api.Admin.GetAsync("/api/v1/leaves?status=cancelled&limit=100");

        created.Status.Should().Be(HttpStatusCode.Created, created.Raw);
        denied.Status.Should().Be(HttpStatusCode.Forbidden);
        cancelled.Data.GetProperty("status").GetString().Should().Be("cancelled");
        submitted.Items.Select(i => i.GetProperty("id").GetGuid()).Should().NotContain(id);
        cancelledList.Items.Select(i => i.GetProperty("id").GetGuid()).Should().Contain(id);
    }

    [Fact]
    public async Task A_teacher_cannot_request_leave_for_a_child_outside_their_rooms()
    {
        var type = await LeaveTypeAsync("Other");
        var teacher = await api.CreateUserAsync("teacher");
        var outsider = await ChildAsync();

        var reply = await teacher.Client.PostAsync("/api/v1/leaves", new
        {
            person_type = "student", person_id = outsider, leave_type_id = type, start_date = Day(2), end_date = Day(2),
        });

        reply.Status.Should().Be(HttpStatusCode.NotFound);
    }
}
