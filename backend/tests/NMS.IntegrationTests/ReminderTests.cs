using System.Net;
using FluentAssertions;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ReminderTests(NmsApiFixture api)
{
    private static string Day(int offset) => DateTime.UtcNow.AddHours(4).AddDays(offset).ToString("yyyy-MM-dd");

    private async Task<(Guid Child, Guid Invoice, NMS.IntegrationTests.Support.ApiClient Parent)> FamilyInvoiceAsync(int dueInDays)
    {
        var child = await api.CreateStudentAsync(await api.CreateRoomAsync(), $"fam-{Guid.NewGuid():N}@test.local");
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardian);
        var created = await api.Admin.PostAsync("/api/v1/invoices", new
        {
            student_id = child, period_start = Day(-30), period_end = Day(0), issue = true, issue_date = Day(-20), due_date = Day(dueInDays),
            lines = new[] { new { description = new { en = "Fees" }, quantity = 1, unit_price_fils = 100_000, tax_rate_percent = (decimal?)null } },
        });
        created.Status.Should().Be(HttpStatusCode.Created, created.Raw);
        return (child, created.Data.GetProperty("invoice").GetProperty("id").GetGuid(), parent.Client);
    }

    private async Task<List<System.Text.Json.JsonElement>> LogFor(Guid invoice) =>
        (await api.Admin.GetAsync("/api/v1/billing/reminders?limit=100")).Items.Where(i => i.GetProperty("invoice_id").GetGuid() == invoice).ToList();

    [Fact]
    public async Task A_reminder_goes_to_the_family_on_the_chosen_day_and_is_never_sent_twice()
    {
        await api.Admin.PutAsync("/api/v1/billing/settings", new { due_days = 14, reminders_enabled = true, before_due_days = new[] { 3, 1 }, after_due_days = new[] { 1, 7 } });
        var (_, invoice, parent) = await FamilyInvoiceAsync(dueInDays: 3);

        (await api.Admin.PostAsync("/api/v1/billing/reminders/actions/run", new { })).Status.Should().Be(HttpStatusCode.OK);
        (await api.Admin.PostAsync("/api/v1/billing/reminders/actions/run", new { })).Status.Should().Be(HttpStatusCode.OK);

        var log = await LogFor(invoice);
        log.Should().ContainSingle();
        log[0].GetProperty("kind").GetString().Should().Be("before_due");
        log[0].GetProperty("offset_days").GetInt32().Should().Be(3);
        log[0].GetProperty("recipients").GetInt32().Should().Be(1);
        // Before-due reminders are informational, so they go by e-mail (not the in-app inbox) unless the parent chooses otherwise.
        (await parent.GetAsync("/api/v1/notifications?limit=50")).Items.Should().NotContain(n => n.GetProperty("type").GetString() == "fee_reminder");
    }

    [Fact]
    public async Task An_unpaid_invoice_is_chased_after_its_due_date_and_a_paid_one_is_left_alone()
    {
        await api.Admin.PutAsync("/api/v1/billing/settings", new { due_days = 14, reminders_enabled = true, before_due_days = new[] { 3, 1 }, after_due_days = new[] { 1, 7 } });
        var (_, late, parent) = await FamilyInvoiceAsync(dueInDays: -1);
        var (_, paid, _) = await FamilyInvoiceAsync(dueInDays: -1);
        (await api.Admin.PostAsync($"/api/v1/invoices/{paid}/payments", new { amount_fils = 100_000, payment_date = Day(0), method = "cash" })).Status.Should().Be(HttpStatusCode.Created);

        await api.Admin.PostAsync("/api/v1/billing/reminders/actions/run", new { });

        (await LogFor(late)).Should().ContainSingle(l => l.GetProperty("kind").GetString() == "overdue");
        (await LogFor(paid)).Should().BeEmpty();
        (await parent.GetAsync("/api/v1/notifications?limit=50")).Items.Should().Contain(n => n.GetProperty("type").GetString() == "fee_overdue");
    }

    [Fact]
    public async Task Switching_reminders_off_sends_nothing_and_the_settings_are_checked_and_for_billing_roles_only()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var bad = await api.Admin.PutAsync("/api/v1/billing/settings", new { due_days = 14, reminders_enabled = true, before_due_days = new[] { 0 }, after_due_days = new[] { 1 } });
        var denied = await teacher.Client.GetAsync("/api/v1/billing/settings");
        var off = await api.Admin.PutAsync("/api/v1/billing/settings", new { due_days = 21, reminders_enabled = false, before_due_days = new[] { 3 }, after_due_days = new[] { 1 } });
        var (_, invoice, _) = await FamilyInvoiceAsync(dueInDays: 3);

        await api.Admin.PostAsync("/api/v1/billing/reminders/actions/run", new { });
        await api.Admin.PutAsync("/api/v1/billing/settings", new { due_days = 14, reminders_enabled = true, before_due_days = new[] { 3, 1 }, after_due_days = new[] { 1, 7 } });

        bad.Status.Should().Be(HttpStatusCode.BadRequest);
        denied.Status.Should().Be(HttpStatusCode.Forbidden);
        off.Data.GetProperty("due_days").GetInt32().Should().Be(21);
        (await LogFor(invoice)).Should().BeEmpty();
    }
}
