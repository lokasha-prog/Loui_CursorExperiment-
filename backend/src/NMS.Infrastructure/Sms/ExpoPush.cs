using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NMS.Application.Notifications;
using NMS.Shared.Results;

namespace NMS.Infrastructure.Sms;

public sealed class PushOptions
{
    public const string Section = "Push";

    /// <summary>"expo" or empty (off). Expo's push service needs no key for ordinary sending.</summary>
    public string Provider { get; set; } = string.Empty;
}

/// <summary>Sends a phone alert through Expo's push service. Off until <c>Push:Provider</c> is set to "expo".</summary>
public sealed class ExpoPushSender(HttpClient http, IOptions<PushOptions> options) : IPushSender
{
    public bool IsConfigured => options.Value.Provider == "expo";

    public async Task<Result> SendAsync(string token, string title, string body, string tier, string? link, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return Result.Failure(Error.InvalidState("Phone alerts are not set up."));
        }

        try
        {
            using var response = await http.PostAsJsonAsync("https://exp.host/--/api/v2/push/send", new { to = token, title, body, sound = "default", priority = tier == "critical" ? "high" : "normal", channelId = tier, data = new { link, tier } }, cancellationToken);
            return response.IsSuccessStatusCode ? Result.Success() : Result.Failure(Error.InvalidState($"Push service answered {(int)response.StatusCode}."));
        }
        catch (HttpRequestException)
        {
            return Result.Failure(Error.InvalidState("The push service could not be reached."));
        }
    }
}
