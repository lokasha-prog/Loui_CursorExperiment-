using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NMS.Application.Enquiries;

namespace NMS.Infrastructure.Captcha;

public sealed class CaptchaOptions
{
    public const string Section = "Captcha";

    /// <summary>The reCAPTCHA secret key. Keep it in user-secrets or the server's environment, never in the repository. Empty turns the check off.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>The public site key shown to the browser. It is not a secret.</summary>
    public string SiteKey { get; set; } = string.Empty;
}

public sealed class RecaptchaVerifier(HttpClient http, IOptions<CaptchaOptions> options) : ICaptchaVerifier
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.SecretKey);

    public async Task<bool> VerifyAsync(string? token, string? ipAddress, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = options.Value.SecretKey, ["response"] = token, ["remoteip"] = ipAddress ?? string.Empty,
            });
            using var response = await http.PostAsync("https://www.google.com/recaptcha/api/siteverify", form, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<Reply>(cancellationToken);
            return body?.Success == true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private sealed record Reply([property: JsonPropertyName("success")] bool Success);
}
