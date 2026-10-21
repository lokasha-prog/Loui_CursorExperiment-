using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NMS.Application.Payments;
using NMS.Infrastructure.Payments;
using NMS.IntegrationTests.Support;
using Xunit;

namespace NMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class OnlinePaymentTests(NmsApiFixture api)
{
    private async Task<(Guid Invoice, ApiClient Parent)> FamilyInvoiceAsync(long price = 100_000)
    {
        var email = $"fam-{Guid.NewGuid():N}@test.local";
        var child = await api.CreateStudentAsync(await api.CreateRoomAsync(), email);
        await api.Admin.PostAsync($"/api/v1/students/{child}/actions/transition-status", new { status = "active" });
        var guardian = (await api.Admin.GetAsync($"/api/v1/students/{child}/guardians")).Items.Single().GetProperty("id").GetGuid();
        var parent = await api.CreateUserAsync("parent", guardianId: guardian);
        var made = await api.Admin.PostAsync("/api/v1/invoices", new
        {
            student_id = child, period_start = DateTime.UtcNow.ToString("yyyy-MM-01"), period_end = DateTime.UtcNow.AddMonths(1).ToString("yyyy-MM-01"), issue = true,
            lines = new[] { new { description = new { en = "Tuition" }, quantity = 1, unit_price_fils = price, tax_rate_percent = (decimal?)null } },
        });
        made.Status.Should().Be(HttpStatusCode.Created, made.Raw);
        return (made.Data.GetProperty("invoice").GetProperty("id").GetGuid(), parent.Client);
    }

    private async Task<(Guid Session, string Reference, string PagePath)> StartAsync(ApiClient parent, Guid invoice, object? body = null)
    {
        var started = await parent.PostAsync($"/api/v1/billing/invoices/{invoice}/pay", body ?? new { });
        started.Status.Should().Be(HttpStatusCode.Created, started.Raw);
        var url = new Uri(started.Data.GetProperty("redirect_url").GetString()!);
        return (started.Data.GetProperty("session_id").GetGuid(), started.Data.GetProperty("reference").GetString()!, url.AbsolutePath);
    }

    private async Task PressAsync(string pagePath, string outcome, bool saveCard = false)
    {
        var fields = new List<KeyValuePair<string, string>> { new("outcome", outcome) };
        if (saveCard)
        {
            fields.Add(new("save_card", "on"));
        }

        var client = api.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var page = await client.GetAsync(pagePath);
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        (await page.Content.ReadAsStringAsync()).Should().Contain("TEST ONLY");
        var sent = await client.PostAsync(pagePath, new FormUrlEncodedContent(fields));
        sent.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var back = await client.GetAsync(sent.Headers.Location!.ToString());
        back.StatusCode.Should().Be(HttpStatusCode.Redirect);
        back.Headers.Location!.ToString().Should().Contain("payment=");
    }

    private async Task<HttpStatusCode> WebhookAsync(string reference, string status, long amount, string? eventId = null, string? signature = null, long? timestamp = null)
    {
        var body = JsonSerializer.Serialize(new
        {
            event_id = eventId ?? "EVT-" + Guid.NewGuid().ToString("N"), reference, status, amount_fils = amount, gateway_reference = "SBX-" + reference[^8..],
            timestamp = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
        var client = api.NewClient();
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhooks/sandbox") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        message.Headers.Add(SandboxGateway.SignatureHeader, signature ?? SandboxGateway.Sign(api.PaymentSecret, body));
        return (await api.CreateClient().SendAsync(message)).StatusCode;
    }

    private async Task<JsonElement> InvoiceAsync(Guid id) => (await api.Admin.GetAsync($"/api/v1/invoices/{id}")).Data.GetProperty("invoice");

    [Fact]
    public async Task A_parent_pays_an_invoice_online_and_it_is_recorded_as_a_card_payment()
    {
        var (invoice, parent) = await FamilyInvoiceAsync();
        (await parent.GetAsync("/api/v1/billing/payments/config")).Data.GetProperty("enabled").GetBoolean().Should().BeTrue();

        var (session, reference, page) = await StartAsync(parent, invoice);
        (await InvoiceAsync(invoice)).GetProperty("paid_fils").GetInt64().Should().Be(0);
        await PressAsync(page, "paid");

        var inv = await InvoiceAsync(invoice);
        inv.GetProperty("status").GetString().Should().Be("paid");
        inv.GetProperty("paid_fils").GetInt64().Should().Be(inv.GetProperty("total_fils").GetInt64());
        var detail = await api.Admin.GetAsync($"/api/v1/invoices/{invoice}");
        detail.Data.GetProperty("payments").EnumerateArray().Should().ContainSingle(p => p.GetProperty("method").GetString() == "card");

        var mine = await parent.GetAsync($"/api/v1/billing/payments/by-reference/{reference}");
        mine.Data.GetProperty("status").GetString().Should().Be("paid");
        (await parent.GetAsync("/api/v1/billing/payments")).Items.Should().ContainSingle(s => s.GetProperty("id").GetGuid() == session);
        (await parent.PostAsync($"/api/v1/billing/invoices/{invoice}/pay", new { })).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_declined_or_cancelled_payment_leaves_the_invoice_open_and_can_be_tried_again()
    {
        var (invoice, parent) = await FamilyInvoiceAsync();
        var first = await StartAsync(parent, invoice);
        await PressAsync(first.PagePath, "failed");
        var second = await StartAsync(parent, invoice);
        await PressAsync(second.PagePath, "cancelled");

        (await InvoiceAsync(invoice)).GetProperty("paid_fils").GetInt64().Should().Be(0);
        (await parent.GetAsync($"/api/v1/billing/payments/by-reference/{first.Reference}")).Data.GetProperty("status").GetString().Should().Be("failed");
        (await parent.GetAsync($"/api/v1/billing/payments/by-reference/{second.Reference}")).Data.GetProperty("status").GetString().Should().Be("cancelled");

        var third = await StartAsync(parent, invoice, new { amount_fils = 40_000 });
        await PressAsync(third.PagePath, "paid");
        var inv = await InvoiceAsync(invoice);
        inv.GetProperty("paid_fils").GetInt64().Should().Be(40_000);
        inv.GetProperty("status").GetString().Should().Be("partially_paid");
    }

    [Fact]
    public async Task A_confirmation_must_be_signed_fresh_and_is_applied_only_once()
    {
        var (invoice, parent) = await FamilyInvoiceAsync(50_000);
        var (_, reference, _) = await StartAsync(parent, invoice);
        var total = (await InvoiceAsync(invoice)).GetProperty("total_fils").GetInt64();

        (await WebhookAsync(reference, "paid", total, signature: "deadbeef")).Should().Be(HttpStatusCode.Unauthorized);
        (await WebhookAsync(reference, "paid", total, timestamp: DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds())).Should().Be(HttpStatusCode.Unauthorized);
        (await InvoiceAsync(invoice)).GetProperty("paid_fils").GetInt64().Should().Be(0);

        (await WebhookAsync(reference, "paid", total, "EVT-SAME")).Should().Be(HttpStatusCode.OK);
        (await WebhookAsync(reference, "paid", total, "EVT-SAME")).Should().Be(HttpStatusCode.OK);
        (await WebhookAsync(reference, "paid", total, "EVT-OTHER")).Should().Be(HttpStatusCode.OK);
        (await InvoiceAsync(invoice)).GetProperty("paid_fils").GetInt64().Should().Be(total);

        var unknown = NMS.Application.Payments.PaymentGateways.NewReference(api.TenantId);
        (await WebhookAsync(unknown, "paid", 1)).Should().Be(HttpStatusCode.NotFound);
        (await api.NewClient().PostAsync("/api/v1/payments/webhooks/nonsense", new { })).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_wrong_amount_or_an_invoice_settled_meanwhile_is_held_for_a_person_not_applied()
    {
        var (invoice, parent) = await FamilyInvoiceAsync(50_000);
        var (session, reference, _) = await StartAsync(parent, invoice);
        (await WebhookAsync(reference, "paid", 1)).Should().Be(HttpStatusCode.OK);
        (await parent.GetAsync($"/api/v1/billing/payments/by-reference/{reference}")).Data.GetProperty("status").GetString().Should().Be("needs_review");
        (await InvoiceAsync(invoice)).GetProperty("paid_fils").GetInt64().Should().Be(0);

        var (invoice2, parent2) = await FamilyInvoiceAsync(50_000);
        var second = await StartAsync(parent2, invoice2);
        var total = (await InvoiceAsync(invoice2)).GetProperty("total_fils").GetInt64();
        (await api.Admin.PostAsync($"/api/v1/invoices/{invoice2}/payments", new { amount_fils = total, payment_date = DateTime.UtcNow.AddHours(4).ToString("yyyy-MM-dd"), method = "cash" })).Status.Should().Be(HttpStatusCode.Created);
        (await WebhookAsync(second.Reference, "paid", total)).Should().Be(HttpStatusCode.OK);
        (await api.Admin.GetAsync("/api/v1/billing/payments?status=needs_review")).Items.Select(s => s.GetProperty("id").GetGuid()).Should().Contain([session, second.Session]);

        var check = await api.Admin.GetAsync("/api/v1/billing/payments/reconciliation");
        check.Data.GetProperty("needs_review_count").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        check.Data.GetProperty("issues").EnumerateArray().Select(i => i.GetProperty("session_id").GetGuid()).Should().Contain(session);
    }

    [Fact]
    public async Task Another_family_and_staff_without_billing_cannot_start_or_see_a_payment()
    {
        var (invoice, parent) = await FamilyInvoiceAsync();
        var (_, other) = await FamilyInvoiceAsync();
        var teacher = await api.CreateUserAsync("teacher");
        var (_, reference, _) = await StartAsync(parent, invoice);

        (await other.PostAsync($"/api/v1/billing/invoices/{invoice}/pay", new { })).Status.Should().Be(HttpStatusCode.NotFound);
        (await other.GetAsync($"/api/v1/billing/payments/by-reference/{reference}")).Status.Should().Be(HttpStatusCode.NotFound);
        (await teacher.Client.PostAsync($"/api/v1/billing/invoices/{invoice}/pay", new { })).Status.Should().Be(HttpStatusCode.Forbidden);
        (await parent.PostAsync($"/api/v1/billing/invoices/{invoice}/pay", new { amount_fils = 999_999_999 })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await parent.GetAsync("/api/v1/billing/payments/reconciliation")).Status.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Accounts_refund_part_or_all_of_a_payment_and_the_invoice_goes_back_to_owing()
    {
        var (invoice, parent) = await FamilyInvoiceAsync(100_000);
        var (session, _, page) = await StartAsync(parent, invoice);
        await PressAsync(page, "paid");
        var total = (await InvoiceAsync(invoice)).GetProperty("total_fils").GetInt64();

        (await parent.PostAsync($"/api/v1/billing/payments/{session}/refund", new { reason = "x" })).Status.Should().Be(HttpStatusCode.Forbidden);
        (await api.Admin.PostAsync($"/api/v1/billing/payments/{session}/refund", new { amount_fils = total + 1, reason = "Too much" })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await api.Admin.PostAsync($"/api/v1/billing/payments/{session}/refund", new { amount_fils = 30_000 })).Status.Should().Be(HttpStatusCode.BadRequest);

        var part = await api.Admin.PostAsync($"/api/v1/billing/payments/{session}/refund", new { amount_fils = 30_000, reason = "Overcharged" });
        part.Status.Should().Be(HttpStatusCode.OK, part.Raw);
        part.Data.GetProperty("status").GetString().Should().Be("partially_refunded");
        var inv = await InvoiceAsync(invoice);
        inv.GetProperty("paid_fils").GetInt64().Should().Be(total - 30_000);
        inv.GetProperty("status").GetString().Should().NotBe("paid");

        var rest = await api.Admin.PostAsync($"/api/v1/billing/payments/{session}/refund", new { reason = "Family left" });
        rest.Data.GetProperty("status").GetString().Should().Be("refunded");
        (await InvoiceAsync(invoice)).GetProperty("paid_fils").GetInt64().Should().Be(0);
        (await api.Admin.PostAsync($"/api/v1/billing/payments/{session}/refund", new { reason = "Again" })).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_card_can_be_kept_and_used_next_time_and_removed_by_its_owner()
    {
        var (invoice, parent) = await FamilyInvoiceAsync(100_000);
        var (_, _, page) = await StartAsync(parent, invoice, new { amount_fils = 40_000, save_card = true });
        await PressAsync(page, "paid", saveCard: true);

        var methods = await parent.GetAsync("/api/v1/billing/payment-methods");
        var kept = methods.Items.Should().ContainSingle().Subject;
        kept.GetProperty("last4").GetString().Should().HaveLength(4);
        kept.ToString().Should().NotContain("SBX-TOK");

        var id = kept.GetProperty("id").GetGuid();
        var again = await parent.PostAsync($"/api/v1/billing/invoices/{invoice}/pay", new { amount_fils = 20_000, saved_method_id = id });
        again.Status.Should().Be(HttpStatusCode.Created, again.Raw);
        again.Data.GetProperty("status").GetString().Should().Be("paid");
        (await InvoiceAsync(invoice)).GetProperty("paid_fils").GetInt64().Should().Be(60_000);

        var (_, otherParent) = await FamilyInvoiceAsync();
        (await otherParent.GetAsync("/api/v1/billing/payment-methods")).Items.Should().BeEmpty();
        (await otherParent.DeleteAsync($"/api/v1/billing/payment-methods/{id}")).Status.Should().Be(HttpStatusCode.NotFound);
        (await parent.DeleteAsync($"/api/v1/billing/payment-methods/{id}")).Status.Should().Be(HttpStatusCode.NoContent);
        (await parent.GetAsync("/api/v1/billing/payment-methods")).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task The_daily_check_applies_a_payment_the_confirmation_never_reached_us_for()
    {
        var (invoice, parent) = await FamilyInvoiceAsync(80_000);
        var (_, reference, _) = await StartAsync(parent, invoice);
        var total = (await InvoiceAsync(invoice)).GetProperty("total_fils").GetInt64();

        // The payment company recorded a payment but its message to us was lost.
        api.Services.GetRequiredService<SandboxLedger>().Record(reference, new GatewayStatus(GatewayOutcomes.Paid, total, null, null));
        using (var scope = api.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<NMS.Application.Abstractions.ITenantContext>().TenantId = api.TenantId;
            var done = await scope.ServiceProvider.GetRequiredService<MediatR.ISender>().Send(new RunPaymentReconciliationCommand());
            done.IsSuccess.Should().BeTrue();
        }

        (await InvoiceAsync(invoice)).GetProperty("status").GetString().Should().Be("paid");
        (await api.Admin.GetAsync($"/api/v1/billing/payments/by-reference/{reference}")).Data.GetProperty("status").GetString().Should().Be("paid");
    }
}
