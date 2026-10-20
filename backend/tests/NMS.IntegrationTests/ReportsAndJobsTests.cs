using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NMS.Api.Jobs;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ReportsAndJobsTests(NmsApiFixture api)
{
    private static string ThisMonth => DateTime.UtcNow.AddHours(4).ToString("yyyy-MM");

    private async Task<(Guid Id, string Barcode)> ActiveChildAsync(Guid? room = null, string first = "Layla")
    {
        var id = await api.CreateStudentAsync(room ?? await api.CreateRoomAsync(), null, first);
        await api.Admin.PostAsync($"/api/v1/students/{id}/actions/transition-status", new { status = "active" });
        return (id, (await api.Admin.GetAsync($"/api/v1/students/{id}")).Data.GetProperty("barcode").GetString()!);
    }

    [Fact]
    public async Task Monthly_report_counts_each_childs_days_and_the_csv_neutralises_formulas()
    {
        var present = await ActiveChildAsync(first: "=Evil");
        var absent = await ActiveChildAsync();
        await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode = present.Barcode });
        await api.Admin.PostAsync("/api/v1/attendance/actions/mark-absent", new { student_id = absent.Id });

        var report = await api.Admin.GetAsync($"/api/v1/attendance/report/monthly?month={ThisMonth}");
        var csv = await api.Admin.GetAsync($"/api/v1/attendance/export?month={ThisMonth}");

        report.Status.Should().Be(HttpStatusCode.OK, report.Raw);
        report.Data.GetProperty("rows").EnumerateArray().Should().Contain(r => r.GetProperty("person_id").GetGuid() == absent.Id && r.GetProperty("absent").GetInt32() == 1);
        csv.Status.Should().Be(HttpStatusCode.OK);
        csv.Raw.Should().Contain("\"'=Evil Family").And.NotContain("\"=Evil");
    }

    [Fact]
    public async Task Report_inputs_are_validated_and_restricted_to_managers()
    {
        var teacher = await api.CreateUserAsync("teacher");

        (await api.Admin.GetAsync("/api/v1/attendance/report/monthly?month=nonsense")).Status.Should().Be(HttpStatusCode.BadRequest);
        (await teacher.Client.GetAsync($"/api/v1/attendance/report/monthly?month={ThisMonth}")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await teacher.Client.GetAsync($"/api/v1/attendance/export?month={ThisMonth}")).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Forecast_subtracts_children_on_approved_leave()
    {
        var home = await ActiveChildAsync();
        await ActiveChildAsync();
        var type = (await api.Admin.PostAsync("/api/v1/leave-types", new { name = new { en = $"Trip {Guid.NewGuid():N}" }, applies_to = "student", requires_approval = false })).Data.GetProperty("id").GetGuid();
        var tomorrow = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        var before = (await api.Admin.GetAsync($"/api/v1/attendance/forecast?date={tomorrow}")).Data.GetProperty("total_expected").GetInt32();
        await api.Admin.PostAsync("/api/v1/leaves", new { person_type = "student", person_id = home.Id, leave_type_id = type, start_date = tomorrow, end_date = tomorrow });

        var after = await api.Admin.GetAsync($"/api/v1/attendance/forecast?date={tomorrow}");

        after.Status.Should().Be(HttpStatusCode.OK, after.Raw);
        after.Data.GetProperty("total_expected").GetInt32().Should().Be(before - 1);
    }

    [Fact]
    public async Task A_manager_can_correct_a_record_but_not_turn_an_arrival_into_an_absence()
    {
        var child = await ActiveChildAsync();
        var arrived = await api.Admin.PostAsync("/api/v1/attendance/actions/check-in", new { barcode = child.Barcode });
        var id = arrived.Data.GetProperty("id").GetGuid();
        var teacher = await api.CreateUserAsync("teacher");

        var late = await api.Admin.PatchAsync($"/api/v1/attendance/{id}", new { status = "late", notes = "Traffic" });
        var absent = await api.Admin.PatchAsync($"/api/v1/attendance/{id}", new { status = "absent" });
        var denied = await teacher.Client.PatchAsync($"/api/v1/attendance/{id}", new { status = "present" });

        late.Data.GetProperty("status").GetString().Should().Be("late");
        absent.Status.Should().Be(HttpStatusCode.Conflict);
        denied.Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_daily_job_marks_children_on_excused_leave_absent_once_and_only_once()
    {
        var child = await ActiveChildAsync();
        var type = (await api.Admin.PostAsync("/api/v1/leave-types", new { name = new { en = $"Excused {Guid.NewGuid():N}" }, applies_to = "student", requires_approval = false })).Data.GetProperty("id").GetGuid();
        var today = DateTime.UtcNow.AddHours(4).ToString("yyyy-MM-dd");
        await api.Admin.PostAsync("/api/v1/leaves", new { person_type = "student", person_id = child.Id, leave_type_id = type, start_date = today, end_date = today });
        var jobs = api.Services.GetRequiredService<ScheduledJobsService>();

        await jobs.RunOnceAsync(CancellationToken.None);
        await jobs.RunOnceAsync(CancellationToken.None);
        var list = await api.Admin.GetAsync("/api/v1/attendance?person_type=student&limit=100");

        list.Items.Where(r => r.GetProperty("person_id").GetGuid() == child.Id).Should().ContainSingle()
            .Which.GetProperty("status").GetString().Should().Be("absent");
    }
}
