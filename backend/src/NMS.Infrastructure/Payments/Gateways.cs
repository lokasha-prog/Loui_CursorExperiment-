using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using NMS.Application.Payments;
using NMS.Shared.Results;

namespace NMS.Infrastructure.Payments;

/// <summary>What the pretend payment company "knows": the outcome of each payment, so the daily check can ask it like it would ask a real one.</summary>
public sealed class SandboxLedger
{
    private readonly ConcurrentDictionary<string, GatewayStatus> _outcomes = new();

    public void Record(string reference, GatewayStatus status) => _outcomes[reference] = status;

    public GatewayStatus? Find(string reference) => _outcomes.TryGetValue(reference, out var s) ? s : null;
}

/// <summary>
/// A pretend payment company for development and demos: a simple payment page served by this API, with confirmations signed with a shared secret
/// exactly like a real company would. It only switches on when Payments:Provider is "sandbox" and a secret is set, so it can never take the place of a real gateway by accident.
/// </summary>
public sealed class SandboxGateway(IOptions<PaymentOptions> options, SandboxLedger ledger) : IPaymentGateway
{
    public const string SignatureHeader = "X-Signature";

    public string Name => "sandbox";

    public bool SupportsSavedCards => true;

    public bool IsConfigured => options.Value.Provider == Name && !string.IsNullOrWhiteSpace(options.Value.WebhookSecret);

    public Task<Result<GatewaySessionResult>> CreateSessionAsync(GatewaySessionRequest request, CancellationToken ct)
    {
        var gatewayReference = "SBX-" + request.Reference[^8..];
        if (request.SavedToken is not null)
        {
            // A saved card is charged straight away, with no page.
            var card = new CardInfo(request.SavedToken, "visa", request.SavedToken[^4..], 12, DateTime.UtcNow.Year + 3);
            var status = new GatewayStatus(GatewayOutcomes.Paid, request.AmountFils, card, null);
            ledger.Record(request.Reference, status);
            return Task.FromResult(Result<GatewaySessionResult>.Success(new GatewaySessionResult(gatewayReference, null, GatewayOutcomes.Paid, card)));
        }

        return Task.FromResult(Result<GatewaySessionResult>.Success(
            new GatewaySessionResult(gatewayReference, $"{options.Value.PublicApiUrl.TrimEnd('/')}/pay/sandbox/{request.Reference}", null, null)));
    }

    public Task<GatewayStatus> GetStatusAsync(string? gatewayReference, string reference, CancellationToken ct) =>
        Task.FromResult(ledger.Find(reference) ?? new GatewayStatus(GatewayOutcomes.Pending, 0, null, null));

    public Task<Result<string>> RefundAsync(string? gatewayReference, string reference, long amountFils, string currency, string reason, CancellationToken ct) =>
        Task.FromResult(Result<string>.Success("SBX-REF-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(5))));

    public Result<GatewayEventData> ParseWebhook(IReadOnlyDictionary<string, string> headers, string body)
    {
        if (!headers.TryGetValue(SignatureHeader, out var signature) || !SignatureMatches(options.Value.WebhookSecret, body, signature))
        {
            return Error.Unauthorized("Bad signature.");
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var stamp = DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("timestamp").GetInt64());
            if (Math.Abs((DateTimeOffset.UtcNow - stamp).TotalMinutes) > 10)
            {
                return Error.Unauthorized("Message too old.");
            }

            CardInfo? card = null;
            if (root.TryGetProperty("card", out var c) && c.ValueKind == JsonValueKind.Object)
            {
                card = new CardInfo(c.GetProperty("token").GetString()!, c.GetProperty("brand").GetString()!, c.GetProperty("last4").GetString()!, c.GetProperty("exp_month").GetInt32(), c.GetProperty("exp_year").GetInt32());
            }

            return Result<GatewayEventData>.Success(new GatewayEventData(
                root.GetProperty("event_id").GetString()!, root.GetProperty("reference").GetString()!, root.GetProperty("status").GetString()!,
                root.GetProperty("amount_fils").GetInt64(), root.TryGetProperty("gateway_reference", out var g) ? g.GetString() : null, card, false));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return Error.Validation("Unreadable message.");
        }
    }

    public static string Sign(string secret, string body) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

    private static bool SignatureMatches(string secret, string body, string signature) =>
        !string.IsNullOrEmpty(secret) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Sign(secret, body)), Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
}

/// <summary>
/// Telr hosted payment page. Written from Telr's published API description and checked against a fake server in tests;
/// it has NOT been run against a real Telr store yet. Needs Payments:TelrStoreId, TelrAuthKey and TelrRemoteKey, and Telr's test mode for the first run.
/// Telr does not sign its messages here, so every message is only a prompt: the system asks Telr directly what happened before it trusts anything.
/// </summary>
public sealed class TelrGateway(HttpClient http, IOptions<PaymentOptions> options) : IPaymentGateway
{
    public const string OrderUrl = "https://secure.telr.com/gateway/order.json";
    public const string RemoteUrl = "https://secure.telr.com/gateway/remote.xml";

    public string Name => "telr";

    public bool SupportsSavedCards => false;

    public bool IsConfigured =>
        options.Value.Provider == Name && !string.IsNullOrWhiteSpace(options.Value.TelrStoreId) && !string.IsNullOrWhiteSpace(options.Value.TelrAuthKey) && !string.IsNullOrWhiteSpace(options.Value.TelrRemoteKey);

    private static string Amount(long fils) => (fils / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    public async Task<Result<GatewaySessionResult>> CreateSessionAsync(GatewaySessionRequest request, CancellationToken ct)
    {
        var o = options.Value;
        var parts = request.PayerName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var body = new
        {
            method = "create", store = o.TelrStoreId, authkey = o.TelrAuthKey, framed = 0,
            order = new { cartid = request.Reference, test = o.TelrTestMode ? "1" : "0", amount = Amount(request.AmountFils), currency = request.Currency, description = request.Description },
            @return = new { authorised = request.ReturnUrl, declined = request.ReturnUrl, cancelled = request.ReturnUrl },
            customer = new { email = request.PayerEmail, name = new { forenames = parts.ElementAtOrDefault(0) ?? string.Empty, surname = parts.ElementAtOrDefault(1) ?? string.Empty } },
        };
        try
        {
            using var response = await http.PostAsJsonAsync(OrderUrl, body, ct);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (json.TryGetProperty("order", out var order) && order.TryGetProperty("url", out var url) && order.TryGetProperty("ref", out var reference))
            {
                return Result<GatewaySessionResult>.Success(new GatewaySessionResult(reference.GetString()!, url.GetString(), null, null));
            }

            return Error.InvalidState("The payment company did not start the payment. Please try again later.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return Error.InvalidState("The payment company could not be reached. Please try again later.");
        }
    }

    public async Task<GatewayStatus> GetStatusAsync(string? gatewayReference, string reference, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(gatewayReference))
        {
            return new GatewayStatus(GatewayOutcomes.Pending, 0, null, null);
        }

        var o = options.Value;
        try
        {
            using var response = await http.PostAsJsonAsync(OrderUrl, new { method = "check", store = o.TelrStoreId, authkey = o.TelrAuthKey, order = new { @ref = gatewayReference } }, ct);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var order = json.GetProperty("order");
            var code = order.GetProperty("status").GetProperty("code").GetInt32();
            var amount = decimal.Parse(order.GetProperty("amount").GetString()!, CultureInfo.InvariantCulture);
            var fils = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
            var cart = order.TryGetProperty("cartid", out var c) ? c.GetString() : null;
            if (cart is not null && cart != reference)
            {
                return new GatewayStatus(GatewayOutcomes.Failed, fils, null, "The payment belongs to a different order.");
            }

            // Telr status codes: 2 authorised, 3 paid, -1 expired, -2 cancelled, -3 declined.
            return code switch
            {
                2 or 3 => new GatewayStatus(GatewayOutcomes.Paid, fils, null, null),
                -2 => new GatewayStatus(GatewayOutcomes.Cancelled, fils, null, "The payment was cancelled."),
                -1 or -3 => new GatewayStatus(GatewayOutcomes.Failed, fils, null, "The card was declined or the payment expired."),
                _ => new GatewayStatus(GatewayOutcomes.Pending, fils, null, null),
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or FormatException or TaskCanceledException)
        {
            return new GatewayStatus(GatewayOutcomes.Pending, 0, null, null);
        }
    }

    public async Task<Result<string>> RefundAsync(string? gatewayReference, string reference, long amountFils, string currency, string reason, CancellationToken ct)
    {
        var o = options.Value;
        var xml = new XElement("remote",
            new XElement("store", o.TelrStoreId), new XElement("key", o.TelrRemoteKey),
            new XElement("tran",
                new XElement("type", "refund"), new XElement("class", "ecom"), new XElement("cartid", reference + "-r"),
                new XElement("description", reason), new XElement("test", o.TelrTestMode ? "1" : "0"),
                new XElement("currency", currency), new XElement("amount", Amount(amountFils)), new XElement("ref", gatewayReference)));
        try
        {
            using var content = new StringContent(xml.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");
            using var response = await http.PostAsync(RemoteUrl, content, ct);
            var reply = XDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var auth = reply.Root?.Element("auth");
            if (auth?.Element("status")?.Value == "A")
            {
                return Result<string>.Success(auth.Element("tranref")?.Value ?? reference);
            }

            return Error.InvalidState($"The payment company did not accept the refund: {auth?.Element("message")?.Value ?? "no reason given"}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Xml.XmlException or TaskCanceledException)
        {
            return Error.InvalidState("The payment company could not be reached. Try the refund again later.");
        }
    }

    public Result<GatewayEventData> ParseWebhook(IReadOnlyDictionary<string, string> headers, string body)
    {
        // Telr posts the order details as a form or JSON. Only the order number is read; the real answer is fetched from Telr.
        string? cart = null;
        string? tranRef = null;
        try
        {
            if (body.TrimStart().StartsWith('{'))
            {
                using var doc = JsonDocument.Parse(body);
                cart = doc.RootElement.TryGetProperty("tran_cartid", out var c) ? c.GetString() : doc.RootElement.TryGetProperty("cartid", out var c2) ? c2.GetString() : null;
                tranRef = doc.RootElement.TryGetProperty("tran_ref", out var t) ? t.GetString() : null;
            }
            else
            {
                var form = body.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2)).Where(p => p.Length == 2)
                    .ToDictionary(p => Uri.UnescapeDataString(p[0].Replace('+', ' ')), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));
                cart = form.GetValueOrDefault("tran_cartid") ?? form.GetValueOrDefault("cartid");
                tranRef = form.GetValueOrDefault("tran_ref");
            }
        }
        catch (JsonException)
        {
            return Error.Validation("Unreadable message.");
        }

        return string.IsNullOrWhiteSpace(cart)
            ? Error.Validation("No order number.")
            : Result<GatewayEventData>.Success(new GatewayEventData($"{cart}:{tranRef ?? "none"}", cart, GatewayOutcomes.Pending, 0, null, null, true));
    }
}
