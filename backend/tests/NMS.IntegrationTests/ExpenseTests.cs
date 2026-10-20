using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ExpenseTests(NmsApiFixture api)
{
    private static string Today => DateTime.UtcNow.AddHours(4).ToString("yyyy-MM-dd");

    [Fact]
    public async Task Expenses_are_recorded_listed_edited_and_removed_and_only_by_billing_roles()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var created = await api.Admin.PostAsync("/api/v1/expenses", new { expense_date = Today, amount_fils = 250_000, category = "supplies", description = "Art paper" });
        var forbidden = await teacher.Client.PostAsync("/api/v1/expenses", new { expense_date = Today, amount_fils = 100, category = "food", description = "x" });
        var id = created.Data.GetProperty("id").GetGuid();

        var edited = await api.Admin.PatchAsync($"/api/v1/expenses/{id}", new { expense_date = Today, amount_fils = 300_000, category = "supplies", description = "Art paper and glue" });
        var removed = await api.Admin.DeleteAsync($"/api/v1/expenses/{id}");
        var again = await api.Admin.DeleteAsync($"/api/v1/expenses/{id}");

        created.Status.Should().Be(HttpStatusCode.Created, created.Raw);
        forbidden.Status.Should().Be(HttpStatusCode.Forbidden);
        edited.Data.GetProperty("amount_fils").GetInt64().Should().Be(300_000);
        removed.Status.Should().Be(HttpStatusCode.NoContent);
        again.Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_expense_needs_a_positive_amount_a_known_category_and_a_date_that_is_not_in_the_future()
    {
        var zero = await api.Admin.PostAsync("/api/v1/expenses", new { expense_date = Today, amount_fils = 0, category = "rent", description = "Rent" });
        var odd = await api.Admin.PostAsync("/api/v1/expenses", new { expense_date = Today, amount_fils = 100, category = "holiday", description = "Trip" });
        var future = await api.Admin.PostAsync("/api/v1/expenses", new { expense_date = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd"), amount_fils = 100, category = "rent", description = "Rent" });
        var blank = await api.Admin.PostAsync("/api/v1/expenses", new { expense_date = Today, amount_fils = 100, category = "rent", description = " " });

        zero.Status.Should().Be(HttpStatusCode.BadRequest);
        odd.Status.Should().Be(HttpStatusCode.BadRequest);
        future.Status.Should().Be(HttpStatusCode.BadRequest);
        blank.Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_monthly_report_adds_up_by_category_and_the_dashboard_shows_net()
    {
        var month = "2020-03";
        foreach (var (cat, amount) in new[] { ("rent", 500_000L), ("rent", 100_000L), ("food", 40_000L) })
        {
            (await api.Admin.PostAsync("/api/v1/expenses", new { expense_date = "2020-03-10", amount_fils = amount, category = cat, description = $"{cat} test" })).Status.Should().Be(HttpStatusCode.Created);
        }

        var report = await api.Admin.GetAsync($"/api/v1/expenses/report?month={month}");
        var snapshot = await api.Admin.GetAsync($"/api/v1/dashboard/billing-snapshot?month={month}");
        var byCategory = report.Data.GetProperty("by_category").EnumerateArray().ToList();

        report.Data.GetProperty("total_fils").GetInt64().Should().Be(640_000);
        byCategory[0].GetProperty("category").GetString().Should().Be("rent");
        byCategory[0].GetProperty("total_fils").GetInt64().Should().Be(600_000);
        snapshot.Data.GetProperty("expenses_fils").GetInt64().Should().Be(640_000);
        snapshot.Data.GetProperty("net_fils").GetInt64().Should().Be(-640_000);
    }
}
