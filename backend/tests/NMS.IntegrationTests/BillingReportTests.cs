using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class BillingReportTests(NmsApiFixture api)
{
    private static string Day(int offset) => DateTime.UtcNow.AddHours(4).AddDays(offset).ToString("yyyy-MM-dd");

    [Fact]
    public async Task The_overdue_list_shows_unpaid_late_invoices_longest_first_and_leaves_out_paid_ones()
    {
        var child = await api.CreateStudentAsync(await api.CreateRoomAsync());
        async Task<Guid> Make(int dueOffset)
        {
            var r = await api.Admin.PostAsync("/api/v1/invoices", new
            {
                student_id = child, period_start = Day(-60), period_end = Day(-30), issue = true, issue_date = Day(-50), due_date = Day(dueOffset),
                lines = new[] { new { description = new { en = "Fees" }, quantity = 1, unit_price_fils = 50_000, tax_rate_percent = (decimal?)null } },
            });
            r.Status.Should().Be(HttpStatusCode.Created, r.Raw);
            return r.Data.GetProperty("invoice").GetProperty("id").GetGuid();
        }

        var old = await Make(-30);
        var recent = await Make(-2);
        var paid = await Make(-10);
        await api.Admin.PostAsync($"/api/v1/invoices/{paid}/payments", new { amount_fils = 50_000, payment_date = Day(0), method = "cash" });

        var report = await api.Admin.GetAsync("/api/v1/reports/billing/overdue");
        var mine = report.Data.GetProperty("rows").EnumerateArray().Where(r => r.GetProperty("student_id").GetGuid() == child).ToList();

        mine.Select(r => r.GetProperty("invoice_id").GetGuid()).Should().Equal(old, recent);
        mine[0].GetProperty("days_late").GetInt32().Should().Be(30);
        mine.Sum(r => r.GetProperty("outstanding_fils").GetInt64()).Should().Be(100_000);
    }

    [Fact]
    public async Task The_revenue_report_splits_collections_by_method_and_nets_off_expenses()
    {
        var child = await api.CreateStudentAsync(await api.CreateRoomAsync());
        var invoice = (await api.Admin.PostAsync("/api/v1/invoices", new
        {
            student_id = child, period_start = "2019-01-01", period_end = "2019-01-31", issue = true, issue_date = "2019-01-05", due_date = "2019-01-19",
            lines = new[] { new { description = new { en = "Fees" }, quantity = 1, unit_price_fils = 80_000, tax_rate_percent = (decimal?)null } },
        })).Data.GetProperty("invoice").GetProperty("id").GetGuid();
        await api.Admin.PostAsync($"/api/v1/invoices/{invoice}/payments", new { amount_fils = 30_000, payment_date = "2019-01-10", method = "cash" });
        await api.Admin.PostAsync($"/api/v1/invoices/{invoice}/payments", new { amount_fils = 20_000, payment_date = "2019-01-11", method = "card" });
        await api.Admin.PostAsync("/api/v1/expenses", new { expense_date = "2019-01-15", amount_fils = 15_000, category = "utilities", description = "Electricity" });

        var report = await api.Admin.GetAsync("/api/v1/reports/billing/revenue?month=2019-01");

        report.Data.GetProperty("billed_fils").GetInt64().Should().Be(80_000);
        report.Data.GetProperty("collected_fils").GetInt64().Should().Be(50_000);
        report.Data.GetProperty("expenses_fils").GetInt64().Should().Be(15_000);
        report.Data.GetProperty("net_fils").GetInt64().Should().Be(35_000);
        report.Data.GetProperty("by_method").GetArrayLength().Should().Be(2);
    }
}
