using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using NMS.Application.Notifications;
using NMS.Shared.Results;

namespace NMS.Infrastructure.Sms;

public sealed class SmsOptions
{
    public const string Section = "Sms";

    /// <summary>"twilio" or empty (off). Keep the keys in user-secrets or the server environment, never in the repository.</summary>
    public string Provider { get; set; } = string.Empty;

    public string AccountSid { get; set; } = string.Empty;

    public string AuthToken { get; set; } = string.Empty;

    /// <summary>The sending number or sender name registered with the provider.</summary>
    public string From { get; set; } = string.Empty;
}

/// <summary>Sends a text message through Twilio. When no keys are set it reports itself as not configured and nothing is sent.</summary>
public sealed class TwilioSmsSender(HttpClient http, IOptions<SmsOptions> options) : ISmsSender
{
    public bool IsConfigured => options.Value is { Provider: "twilio", AccountSid.Length: > 0, AuthToken.Length: > 0, From.Length: > 0 };

    public async Task<Result> SendAsync(string to, string text, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return Result.Failure(Error.InvalidState("SMS is not set up."));
        }

        var o = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/Accounts/{o.AccountSid}/Messages.json")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["To"] = to, ["From"] = o.From, ["Body"] = text }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{o.AccountSid}:{o.AuthToken}")));
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode ? Result.Success() : Result.Failure(Error.InvalidState($"The SMS provider refused the message ({(int)response.StatusCode})."));
        }
        catch (HttpRequestException)
        {
            return Result.Failure(Error.InvalidState("The SMS provider could not be reached."));
        }
    }
}
