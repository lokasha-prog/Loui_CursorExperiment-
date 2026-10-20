using System.Net;
using System.Text.Json;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ReportCentreTests(NmsApiFixture api)
{
    private static string Today => DateTime.UtcNow.AddHours(4).ToString("yyyy-MM-dd");

    private static string Month => DateTime.UtcNow.AddHours(4).ToString("yyyy-MM");

    private async Task<(HttpStatusCode Status, byte[] Bytes, string? Type)> RunAsync(ApiClient client, string kind, string format, Dictionary<string, string?>? parameters = null, string? language = null)
    {
        var made = await client.PostAsync("/api/v1/reports/center/runs", new { kind, format, parameters, language });
        made.Status.Should().Be(HttpStatusCode.Accepted, made.Raw);
        var id = made.Data.GetProperty("id").GetGuid();
        for (var i = 0; i < 60; i++)
        {
            var list = await client.GetAsync("/api/v1/reports/center/runs?limit=50");
            var mine = list.Items.First(r => r.GetProperty("id").GetGuid() == id);
            var status = mine.GetProperty("status").GetString();
            if (status == "done") return await client.GetBytesAsync($"/api/v1/reports/center/runs/{id}/file");
            status.Should().NotBe("failed", mine.ToString());
            await Task.Delay(250);
        }

        throw new TimeoutException("The report was not ready in time.");
    }

    [Fact]
    public async Task Every_report_is_made_in_the_background_and_downloads_as_the_right_kind_of_file()
    {
        var kid = await api.CreateStudentAsync(await api.CreateRoomAsync());
        await api.Admin.PostAsync($"/api/v1/students/{kid}/actions/transition-status", new { status = "active" });
        var range = new Dictionary<string, string?> { ["from"] = $"{DateTime.UtcNow.Year}-01-01", ["to"] = Today };

        var attendanceXlsx = await RunAsync(api.Admin, "attendance_students", "excel", new() { ["month"] = Month });
        var attendancePdf = await RunAsync(api.Admin, "attendance_students", "pdf", new() { ["month"] = Month }, "ar");
        var staffXlsx = await RunAsync(api.Admin, "attendance_staff", "excel", new() { ["month"] = Month });
        var billing = await RunAsync(api.Admin, "billing_revenue", "excel", range);
        var enquiries = await RunAsync(api.Admin, "enquiry_conversion", "pdf", range);
        var parents = await RunAsync(api.Admin, "adoption_parents", "excel", range);
        var staffUse = await RunAsync(api.Admin, "adoption_staff", "excel", range);
        var audit = await RunAsync(api.Admin, "audit_log", "json", range);
        var sheet = await RunAsync(api.Admin, "sign_in_sheet", "pdf", new() { ["date"] = Today });

        foreach (var x in new[] { attendanceXlsx, staffXlsx, billing, parents, staffUse })
        {
            x.Type.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            System.Text.Encoding.ASCII.GetString(x.Bytes, 0, 2).Should().Be("PK");
        }

        foreach (var x in new[] { attendancePdf, enquiries, sheet })
        {
            x.Type.Should().Be("application/pdf");
            System.Text.Encoding.ASCII.GetString(x.Bytes, 0, 4).Should().Be("%PDF");
        }

        using var json = JsonDocument.Parse(audit.Bytes);
        json.RootElement.GetProperty("rows").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Bad_requests_and_missing_rights_are_refused_and_each_person_sees_only_their_own_history()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var parent = await api.CreateUserAsync("parent");

        var noMonth = await api.Admin.PostAsync("/api/v1/reports/center/runs", new { kind = "attendance_students", format = "excel", parameters = new Dictionary<string, string?>() });
        var wrongFormat = await api.Admin.PostAsync("/api/v1/reports/center/runs", new { kind = "billing_revenue", format = "pdf", parameters = new Dictionary<string, string?> { ["from"] = "2026-01-01", ["to"] = "2026-02-01" } });
        var backwards = await api.Admin.PostAsync("/api/v1/reports/center/runs", new { kind = "billing_revenue", format = "excel", parameters = new Dictionary<string, string?> { ["from"] = "2026-03-01", ["to"] = "2026-02-01" } });
        var tooLong = await api.Admin.PostAsync("/api/v1/reports/center/runs", new { kind = "billing_revenue", format = "excel", parameters = new Dictionary<string, string?> { ["from"] = "2020-01-01", ["to"] = "2026-02-01" } });
        var unknown = await api.Admin.PostAsync("/api/v1/reports/center/runs", new { kind = "secrets", format = "excel" });
        var teacherAudit = await teacher.Client.PostAsync("/api/v1/reports/center/runs", new { kind = "audit_log", format = "json", parameters = new Dictionary<string, string?> { ["from"] = "2026-01-01", ["to"] = "2026-02-01" } });
        var parentAny = await parent.Client.GetAsync("/api/v1/reports/center/definitions");
        var teacherDefs = await teacher.Client.GetAsync("/api/v1/reports/center/definitions");
        var adminDefs = await api.Admin.GetAsync("/api/v1/reports/center/definitions");

        var mine = await RunAsync(api.Admin, "sign_in_sheet", "pdf");
        var made = await api.Admin.GetAsync("/api/v1/reports/center/runs?limit=50");
        var teacherList = await teacher.Client.GetAsync("/api/v1/reports/center/runs?limit=50");

        foreach (var bad in new[] { noMonth, wrongFormat, backwards, tooLong, unknown }) bad.Status.Should().Be(HttpStatusCode.BadRequest, bad.Raw);
        teacherAudit.Status.Should().Be(HttpStatusCode.Forbidden);
        parentAny.Status.Should().Be(HttpStatusCode.Forbidden);
        teacherDefs.Data.GetArrayLength().Should().BeLessThan(adminDefs.Data.GetArrayLength());
        mine.Status.Should().Be(HttpStatusCode.OK);
        made.Items.Should().NotBeEmpty();
        teacherList.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Someone_else_cannot_download_a_report_they_did_not_ask_for()
    {
        var made = await api.Admin.PostAsync("/api/v1/reports/center/runs", new { kind = "sign_in_sheet", format = "pdf", parameters = new Dictionary<string, string?> { ["date"] = Today } });
        var id = made.Data.GetProperty("id").GetGuid();
        var teacher = await api.CreateUserAsync("teacher");

        var stolen = await teacher.Client.GetBytesAsync($"/api/v1/reports/center/runs/{id}/file");

        stolen.Status.Should().Be(HttpStatusCode.NotFound);
    }
}

[Collection(ApiCollection.Name)]
public class AnalyticsTests(NmsApiFixture api)
{
    private static string Today => DateTime.UtcNow.AddHours(4).ToString("yyyy-MM-dd");

    [Fact]
    public async Task Analytics_show_trends_for_managers_and_leave_out_money_and_enquiries_for_teachers()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var from = $"{DateTime.UtcNow.Year}-01-01";

        var admin = await api.Admin.GetAsync($"/api/v1/reports/analytics?from={from}&to={Today}");
        var staff = await teacher.Client.GetAsync($"/api/v1/reports/analytics?from={from}&to={Today}");
        var backwards = await api.Admin.GetAsync($"/api/v1/reports/analytics?from={Today}&to={from}");
        var tooLong = await api.Admin.GetAsync("/api/v1/reports/analytics?from=2020-01-01&to=2026-01-01");

        admin.Data.GetProperty("revenue").GetArrayLength().Should().BeGreaterThan(0);
        admin.Data.GetProperty("enquiries_by_source").ValueKind.Should().Be(JsonValueKind.Array);
        staff.Status.Should().Be(HttpStatusCode.OK);
        staff.Data.GetProperty("revenue").ValueKind.Should().Be(JsonValueKind.Null);
        staff.Data.GetProperty("enquiries_by_month").ValueKind.Should().Be(JsonValueKind.Null);
        backwards.Status.Should().Be(HttpStatusCode.BadRequest);
        tooLong.Status.Should().Be(HttpStatusCode.BadRequest);
    }
}
