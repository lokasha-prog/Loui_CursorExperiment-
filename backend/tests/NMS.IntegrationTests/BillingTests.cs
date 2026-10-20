using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NMS.Api.Jobs;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class BillingTests(NmsApiFixture api)
{
    private static string Today => DateTime.UtcNow.AddHours(4).ToString("yyyy-MM-dd");

    private async Task<Guid> PlanAsync(string period = "monthly", long tuition = 200_000, decimal? tax = 5, long discount = 0)
    {
        var components = new List<object> { new { name = new { en = "Tuition", ar = "الرسوم الدراسية" }, component_type = "tuition", amount_fils = tuition, tax_rate_percent = tax } };
        if (discount > 0)
        {
            components.Add(new { name = new { en = "Sibling discount" }, component_type = "discount", amount_fils = discount, tax_rate_percent = (decimal?)null });
        }

        var reply = await api.Admin.PostAsync("/api/v1/fee-plans", new { name = new { en = $"Plan {Guid.NewGuid():N}" }, period, components });
        reply.Status.Should().Be(HttpStatusCode.Created, reply.Raw);
        return reply.Data.GetProperty("plan").GetProperty("id").GetGuid();
    }

    private async Task<Guid> ActiveChildAsync()
    {
        var id = await api.CreateStudentAsync(await api.CreateRoomAsync());
        await api.Admin.PostAsync($"/api/v1/students/{id}/actions/transition-status", new { status = "active" });
        return id;
    }

    private async Task AssignAsync(Guid plan, params Guid[] children) =>
        (await api.Admin.PostAsync($"/api/v1/fee-plans/{plan}/students", new { student_ids = children, effective_from = DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM-dd") }))
            .Status.Should().Be(HttpStatusCode.OK);

    private async Task<Guid> ManualInvoiceAsync(Guid child, long price = 100_000, bool issue = true)
    {
        var reply = await api.Admin.PostAsync("/api/v1/invoices", new
        {
            student_id = child, period_start = DateTime.UtcNow.ToString("yyyy-MM-01"), period_end = DateTime.UtcNow.AddMonths(1).ToString("yyyy-MM-01"), issue,
            lines = new[] { new { description = new { en = "Registration" }, quantity = 1, unit_price_fils = price, tax_rate_percent = (decimal?)5 } },
        });
        reply.Status.Should().Be(HttpStatusCode.Created, reply.Raw);
        return reply.Data.GetProperty("invoice").GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task A_fee_plan_needs_a_real_component_and_a_unique_name_and_is_for_billing_roles_only()
    {
        var teacher = await api.CreateUserAsync("teacher");
        var name = $"Gold {Guid.NewGuid():N}";
        var body = new { name = new { en = name }, period = "monthly", components = new[] { new { name = new { en = "Tuition" }, component_type = "tuition", amount_fils = 150_000, tax_rate_percent = (decimal?)5 } } };

        var created = await api.Admin.PostAsync("/api/v1/fee-plans", body);
        var duplicate = await api.Admin.PostAsync("/api/v1/fee-plans", body);
        var empty = await api.Admin.PostAsync("/api/v1/fee-plans", new { name = new { en = "Empty" }, period = "monthly", components = Array.Empty<object>() });
        var badPeriod = await api.Admin.PostAsync("/api/v1/fee-plans", new { name = new { en = "Bad" }, period = "weekly", components = body.components });

        created.Status.Should().Be(HttpStatusCode.Created, created.Raw);
        created.Data.GetProperty("plan").GetProperty("total_per_period_fils").GetInt64().Should().Be(150_000);
        duplicate.Status.Should().Be(HttpStatusCode.Conflict);
        empty.Status.Should().Be(HttpStatusCode.BadRequest);
        badPeriod.Status.Should().Be(HttpStatusCode.BadRequest);
        (await teacher.Client.GetAsync("/api/v1/fee-plans")).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Generating_invoices_bills_each_child_once_per_month_with_tax_discount_and_sequential_numbers()
    {
        var plan = await PlanAsync(tuition: 200_000, tax: 5, discount: 20_000);
        var first = await ActiveChildAsync();
        var second = await ActiveChildAsync();
        await AssignAsync(plan, first, second);

        var run = await api.Admin.PostAsync("/api/v1/invoices/actions/generate", new { });
        var again = await api.Admin.PostAsync("/api/v1/invoices/actions/generate", new { });
        var list = await api.Admin.GetAsync($"/api/v1/invoices?student_id={first}");
        var invoice = await api.Admin.GetAsync($"/api/v1/invoices/{list.Items.Single().GetProperty("id").GetGuid()}");

        run.Data.GetProperty("created").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        again.Data.GetProperty("created").GetInt32().Should().Be(0, "nothing is billed twice");
        var data = invoice.Data;
        data.GetProperty("subtotal_fils").GetInt64().Should().Be(200_000);
        data.GetProperty("discount_fils").GetInt64().Should().Be(20_000);
        data.GetProperty("tax_fils").GetInt64().Should().Be(10_000);
        data.GetProperty("invoice").GetProperty("total_fils").GetInt64().Should().Be(190_000);
        data.GetProperty("invoice").GetProperty("status").GetString().Should().Be("issued");
        data.GetProperty("invoice").GetProperty("invoice_number").GetString().Should().MatchRegex(@"^INV-\d{4}-\d{5}$");
    }

    [Fact]
    public async Task Payments_move_an_invoice_from_issued_to_partly_paid_to_paid_and_never_overpay()
    {
        var child = await ActiveChildAsync();
        var id = await ManualInvoiceAsync(child, price: 100_000);

        var first = await api.Admin.PostAsync($"/api/v1/invoices/{id}/payments", new { amount_fils = 40_000, payment_date = Today, method = "cash" });
        var tooMuch = await api.Admin.PostAsync($"/api/v1/invoices/{id}/payments", new { amount_fils = 999_999, payment_date = Today, method = "cash" });
        var future = await api.Admin.PostAsync($"/api/v1/invoices/{id}/payments", new { amount_fils = 1_000, payment_date = DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-dd"), method = "cash" });
        var rest = await api.Admin.PostAsync($"/api/v1/invoices/{id}/payments", new { amount_fils = 65_000, payment_date = Today, method = "bank_transfer", reference = "TRX-1" });
        var after = await api.Admin.PostAsync($"/api/v1/invoices/{id}/payments", new { amount_fils = 1_000, payment_date = Today, method = "cash" });

        first.Data.GetProperty("invoice").GetProperty("status").GetString().Should().Be("partially_paid");
        tooMuch.Status.Should().Be(HttpStatusCode.BadRequest);
        future.Status.Should().Be(HttpStatusCode.BadRequest);
        rest.Data.GetProperty("invoice").GetProperty("status").GetString().Should().Be("paid");
        rest.Data.GetProperty("payments").GetArrayLength().Should().Be(2);
        after.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Draft_invoices_can_be_issued_and_unpaid_ones_cancelled_but_not_paid_ones()
    {
        var child = await ActiveChildAsync();
        var draft = await ManualInvoiceAsync(child, issue: false);
        var paid = await ManualInvoiceAsync(child, price: 10_000);

        var payDraft = await api.Admin.PostAsync($"/api/v1/invoices/{draft}/payments", new { amount_fils = 1_000, payment_date = Today, method = "cash" });
        var issued = await api.Admin.PostAsync($"/api/v1/invoices/{draft}/actions/issue");
        var issueAgain = await api.Admin.PostAsync($"/api/v1/invoices/{draft}/actions/issue");
        await api.Admin.PostAsync($"/api/v1/invoices/{paid}/payments", new { amount_fils = 5_000, payment_date = Today, method = "cash" });
        var cancelPaid = await api.Admin.PostAsync($"/api/v1/invoices/{paid}/actions/cancel");
        var cancelled = await api.Admin.PostAsync($"/api/v1/invoices/{draft}/actions/cancel");

        payDraft.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        issued.Data.GetProperty("invoice").GetProperty("status").GetString().Should().Be("issued");
        issueAgain.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        cancelPaid.Status.Should().Be(HttpStatusCode.Conflict);
        cancelled.Data.GetProperty("invoice").GetProperty("status").GetString().Should().Be("cancelled");
    }

    [Fact]
    public async Task A_family_sees_only_its_own_childs_issued_invoices_and_cannot_pay_or_create()
    {
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var child = await api.CreateStudentAsync(await api.CreateRoomAsync(), email);
        var other = await ActiveChildAsync();
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardian);
        var mine = await ManualInvoiceAsync(child);
        var draft = await ManualInvoiceAsync(child, issue: false);
        var theirs = await ManualInvoiceAsync(other);

        var list = await parent.Client.GetAsync("/api/v1/invoices?limit=100");
        var own = await parent.Client.GetAsync($"/api/v1/invoices/{mine}");
        var foreign = await parent.Client.GetAsync($"/api/v1/invoices/{theirs}");
        var hidden = await parent.Client.GetAsync($"/api/v1/invoices/{draft}");

        list.Items.Select(i => i.GetProperty("id").GetGuid()).Should().ContainSingle().Which.Should().Be(mine);
        own.Status.Should().Be(HttpStatusCode.OK);
        foreign.Status.Should().Be(HttpStatusCode.NotFound);
        hidden.Status.Should().Be(HttpStatusCode.NotFound);
        (await parent.Client.PostAsync($"/api/v1/invoices/{mine}/payments", new { amount_fils = 1, payment_date = Today, method = "cash" })).Status.Should().Be(HttpStatusCode.Forbidden);
        (await parent.Client.GetAsync("/api/v1/dashboard/billing-snapshot")).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_invoice_pdf_is_real_in_both_languages_and_the_family_gets_a_quiet_email()
    {
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var child = await api.CreateStudentAsync(await api.CreateRoomAsync(), email);
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardian);
        api.Email.Sent.Clear();

        var id = await ManualInvoiceAsync(child);
        var english = await api.Admin.GetBytesAsync($"/api/v1/invoices/{id}/pdf");
        var arabic = await api.Admin.GetBytesAsync($"/api/v1/invoices/{id}/pdf?language=ar");
        var bad = await api.Admin.GetBytesAsync($"/api/v1/invoices/{id}/pdf?language=fr");
        var asParent = await parent.Client.GetBytesAsync($"/api/v1/invoices/{id}/pdf");
        using (var scope = api.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<NMS.Application.Abstractions.ITenantContext>().TenantId = api.TenantId;
            await scope.ServiceProvider.GetRequiredService<MediatR.ISender>().Send(new NMS.Application.Notifications.RunNotificationDispatchCommand());
        }

        System.Text.Encoding.ASCII.GetString(english.Bytes, 0, 4).Should().Be("%PDF");
        arabic.Bytes.Length.Should().BeGreaterThan(1000);
        bad.Status.Should().Be(HttpStatusCode.BadRequest);
        asParent.Status.Should().Be(HttpStatusCode.OK);
        api.Email.Sent.Should().Contain(m => m.To == parent.Email && m.Subject.Contains("New invoice"));
    }

    [Fact]
    public async Task The_billing_snapshot_adds_up_billed_collected_overdue_and_outstanding()
    {
        var child = await ActiveChildAsync();
        var before = (await api.Admin.GetAsync("/api/v1/dashboard/billing-snapshot")).Data;
        var id = await ManualInvoiceAsync(child, price: 100_000);
        await api.Admin.PostAsync($"/api/v1/invoices/{id}/payments", new { amount_fils = 30_000, payment_date = Today, method = "cash" });

        var after = (await api.Admin.GetAsync("/api/v1/dashboard/billing-snapshot")).Data;

        (after.GetProperty("billed_fils").GetInt64() - before.GetProperty("billed_fils").GetInt64()).Should().Be(105_000);
        (after.GetProperty("collected_fils").GetInt64() - before.GetProperty("collected_fils").GetInt64()).Should().Be(30_000);
        (after.GetProperty("outstanding_fils").GetInt64() - before.GetProperty("outstanding_fils").GetInt64()).Should().Be(75_000);
        after.GetProperty("trend").GetArrayLength().Should().Be(6);
        after.GetProperty("recent_payments").EnumerateArray().Should().Contain(p => p.GetProperty("invoice_id").GetGuid() == id);
    }

    [Fact]
    public async Task The_daily_job_marks_late_invoices_overdue_and_they_still_accept_payment()
    {
        var child = await ActiveChildAsync();
        var reply = await api.Admin.PostAsync("/api/v1/invoices", new
        {
            student_id = child, period_start = "2025-01-01", period_end = "2025-01-31", issue = true, issue_date = "2025-01-01", due_date = "2025-01-15",
            lines = new[] { new { description = new { en = "Old fee" }, quantity = 1, unit_price_fils = 50_000, tax_rate_percent = (decimal?)null } },
        });
        var id = reply.Data.GetProperty("invoice").GetProperty("id").GetGuid();
        var jobs = api.Services.GetRequiredService<ScheduledJobsService>();

        await jobs.RunOnceAsync(CancellationToken.None);
        var overdue = (await api.Admin.GetAsync($"/api/v1/invoices/{id}")).Data.GetProperty("invoice").GetProperty("status").GetString();
        var part = await api.Admin.PostAsync($"/api/v1/invoices/{id}/payments", new { amount_fils = 10_000, payment_date = Today, method = "cheque", reference = "CHQ-77" });

        overdue.Should().Be("overdue");
        part.Data.GetProperty("invoice").GetProperty("status").GetString().Should().Be("overdue", "still late until fully paid");
        (await api.Admin.GetAsync("/api/v1/dashboard/billing-snapshot")).Data.GetProperty("top_overdue").EnumerateArray()
            .Should().Contain(t => t.GetProperty("student_id").GetGuid() == child);
    }
}
