using System.Net;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NMS.Api.Http;
using NMS.Application.Payments;
using NMS.Application.Security;
using NMS.Infrastructure.Payments;

namespace NMS.Api.Endpoints;

public sealed record StartPaymentRequest(long? AmountFils, bool? SaveCard, Guid? SavedMethodId);

public sealed record RefundRequest(long? AmountFils, string? Reason);

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder routes)
    {
        var b = routes.MapGroup("/api/v1/billing").WithTags("Payments");

        b.MapGet("/payments/config", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new PaymentConfigQuery(), ct))).RequireAuthorization(Permissions.BillingRead);
        b.MapPost("/invoices/{id:guid}/pay", async (Guid id, StartPaymentRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new StartPaymentCommand(id, body.AmountFils, body.SaveCard ?? false, body.SavedMethodId), ct), StatusCodes.Status201Created))
            .RequireAuthorization(Permissions.BillingRead);
        b.MapGet("/payments", async (string? status, [FromQuery(Name = "invoice_id")] Guid? invoiceId, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new ListPaymentsQuery(status, invoiceId), ct))).RequireAuthorization(Permissions.BillingRead);
        b.MapGet("/payments/by-reference/{reference}", async (string reference, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new PaymentByReferenceQuery(reference), ct))).RequireAuthorization(Permissions.BillingRead);
        b.MapGet("/payments/reconciliation", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new PaymentReconciliationQuery(), ct))).RequireAuthorization(Permissions.PaymentsRecord);
        b.MapPost("/payments/{id:guid}/refund", async (Guid id, RefundRequest body, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new RefundPaymentCommand(id, body.AmountFils, body.Reason ?? string.Empty), ct)))
            .RequireAuthorization(Permissions.PaymentsRefund);
        b.MapGet("/payment-methods", async (HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new MySavedMethodsQuery(), ct))).RequireAuthorization(Permissions.BillingRead);
        b.MapDelete("/payment-methods/{id:guid}", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
            ApiResults.From(http, await s.Send(new DeleteSavedMethodCommand(id), ct))).RequireAuthorization(Permissions.BillingRead);

        // The payment company calls this from its own servers. Nothing here needs a sign-in: the message has to prove it is genuine.
        routes.MapPost("/api/v1/payments/webhooks/{gateway}", async (string gateway, HttpContext http, ISender s, CancellationToken ct) =>
        {
            using var reader = new StreamReader(http.Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(ct);
            var headers = http.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
            var result = await s.Send(new ProcessGatewayEventCommand(gateway, headers, body), ct);
            return result.IsFailure ? ApiResults.Failure(http, result.Errors) : ApiResults.Ok(http, new { received = true });
        }).AllowAnonymous().RequireRateLimiting("anonymous").WithTags("Payments");

        // Where the parent lands after the payment page: the real outcome is asked from the payment company, then the parent goes back to the web app.
        routes.MapGet("/pay/return/{reference}", async (string reference, ISender s, CancellationToken ct) =>
        {
            var back = await s.Send(new PaymentReturnCommand(reference), ct);
            return Results.Redirect(back.IsSuccess ? back.Value : "/", permanent: false);
        }).AllowAnonymous().RequireRateLimiting("anonymous").ExcludeFromDescription();

        MapSandbox(routes);
    }

    private static void MapSandbox(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/pay/sandbox/{reference}", async (string reference, ISender s, CancellationToken ct) =>
        {
            var page = await s.Send(new SandboxPageQuery(reference), ct);
            return page.IsFailure ? Results.NotFound() : Results.Content(Render(page.Value), "text/html; charset=utf-8");
        }).AllowAnonymous().RequireRateLimiting("anonymous").ExcludeFromDescription();

        routes.MapPost("/pay/sandbox/{reference}", async (
            string reference, HttpContext http, ISender s, SandboxLedger ledger, IOptions<PaymentOptions> options, CancellationToken ct) =>
        {
            var page = await s.Send(new SandboxPageQuery(reference), ct);
            if (page.IsFailure)
            {
                return Results.NotFound();
            }

            var form = await http.Request.ReadFormAsync(ct);
            var outcome = form["outcome"].ToString() switch { "paid" => GatewayOutcomes.Paid, "cancelled" => GatewayOutcomes.Cancelled, _ => GatewayOutcomes.Failed };
            var keep = outcome == GatewayOutcomes.Paid && page.Value.CanSaveCard && form["save_card"] == "on";
            var last4 = Random.Shared.Next(1000, 9999).ToString();
            var card = keep ? new { token = "SBX-TOK-" + Guid.NewGuid().ToString("N")[..12] + last4, brand = "visa", last4, exp_month = 12, exp_year = DateTime.UtcNow.Year + 3 } : null;
            var payload = JsonSerializer.Serialize(new
            {
                event_id = "SBX-EVT-" + Guid.NewGuid().ToString("N"), reference, status = outcome, amount_fils = page.Value.AmountFils,
                gateway_reference = "SBX-" + reference[^8..], timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), card,
            });
            ledger.Record(reference, new GatewayStatus(outcome, page.Value.AmountFils,
                card is null ? null : new CardInfo(card.token, card.brand, card.last4, card.exp_month, card.exp_year), outcome == GatewayOutcomes.Paid ? null : "Not completed."));

            // The confirmation goes through exactly the same door a real company would use, signed with the shared secret.
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [SandboxGateway.SignatureHeader] = SandboxGateway.Sign(options.Value.WebhookSecret, payload) };
            await s.Send(new ProcessGatewayEventCommand("sandbox", headers, payload), ct);
            return Results.Redirect($"/pay/return/{reference}", permanent: false);
        }).AllowAnonymous().RequireRateLimiting("anonymous").ExcludeFromDescription().DisableAntiforgery();
    }

    private static string Render(SandboxPageDto p)
    {
        var amount = (p.AmountFils / 100m).ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        var invoice = WebUtility.HtmlEncode(p.InvoiceNumber ?? string.Empty);
        var save = p.CanSaveCard ? "<label><input type=\"checkbox\" name=\"save_card\"> Save this card / حفظ البطاقة</label>" : string.Empty;
        var body = p.Status != "pending"
            ? "<p>This payment is already finished. / انتهت هذه الدفعة بالفعل.</p>"
            : "<form method=\"post\">" + save
                + "<button class=\"go\" name=\"outcome\" value=\"paid\">Pay now / ادفع الآن</button>"
                + "<button class=\"no\" name=\"outcome\" value=\"failed\">Decline the card / رفض البطاقة</button>"
                + "<button class=\"no\" name=\"outcome\" value=\"cancelled\">Cancel / إلغاء</button></form>";
        return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Test payment page</title>"
            + "<style>body{font-family:system-ui,sans-serif;background:#f6f7f9;margin:0;display:grid;place-items:center;min-height:100vh}"
            + "main{background:#fff;border:1px solid #dde2ea;border-radius:16px;padding:28px;max-width:420px;width:calc(100% - 32px)}"
            + ".tag{background:#fde68a;color:#7a4b00;border-radius:999px;padding:2px 10px;font-size:12px;font-weight:700}"
            + "h1{font-size:20px;margin:12px 0 4px}.amt{font-size:32px;font-weight:800;margin:12px 0}"
            + "button{font:inherit;font-weight:700;border:0;border-radius:10px;padding:12px 16px;cursor:pointer;width:100%;margin-top:8px}"
            + ".go{background:#0f766e;color:#fff}.no{background:#e5e7eb;color:#111}label{display:block;margin-top:10px;font-size:14px}</style></head>"
            + "<body><main><span class=\"tag\">TEST ONLY — no real money / تجريبي فقط</span>"
            + $"<h1>Pay invoice {invoice} <span dir=\"rtl\">/ سداد الفاتورة</span></h1><div class=\"amt\">{p.Currency} {amount}</div>"
            + body + "</main></body></html>";
    }
}
