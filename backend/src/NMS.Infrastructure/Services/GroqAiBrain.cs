using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NMS.Application.Ai;
using NMS.Shared.Results;

namespace NMS.Infrastructure.Services;

public sealed class AiOptions
{
    public const string SectionName = "Ai:Groq";

    public string? ApiKey { get; set; }

    public string Model { get; set; } = "openai/gpt-oss-120b";

    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";

    public int TimeoutSeconds { get; set; } = 40;
}

/// <summary>Groq's OpenAI-compatible chat endpoint. The key comes from local secrets and is never logged.</summary>
public sealed class GroqAiBrain(HttpClient http, IOptions<AiOptions> options, ILogger<GroqAiBrain> logger) : IAiBrain
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public string ModelName => options.Value.Model;

    public async Task<Result<string>> CompleteAsync(string system, string user, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return AiErrors.NotSetUp;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = options.Value.Model,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user },
            },
            temperature = 0.3,
            max_completion_tokens = 2048,
            reasoning_effort = "low",
            stream = false,
        });

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("AI provider answered {Status}", (int)response.StatusCode);
                return Error.InvalidState("The AI Brain is not available right now.");
            }

            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var text = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return string.IsNullOrWhiteSpace(text)
                ? Error.InvalidState("The AI Brain gave no answer.")
                : Result<string>.Success(text);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning("AI provider call failed: {Type}", ex.GetType().Name);
            return Error.InvalidState("The AI Brain is not available right now.");
        }
    }

    /// <summary>The most tool rounds one question may take before the model must answer.</summary>
    private const int MaxRounds = 6;

    public async Task<Result<string>> ChatAsync(
        string system, IReadOnlyList<AiMessage> messages, IReadOnlyList<AiTool> tools,
        Func<string, string, CancellationToken, Task<string>> runTool, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return AiErrors.NotSetUp;
        }

        var conversation = new List<object> { new { role = "system", content = system } };
        conversation.AddRange(messages.Select(m => (object)new { role = m.Role, content = m.Content }));
        var toolList = tools.Select(t => new { type = "function", function = new { name = t.Name, description = t.Description, parameters = t.Parameters } }).ToList();

        try
        {
            for (var round = 0; round <= MaxRounds; round++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
                request.Content = JsonContent.Create(new
                {
                    model = options.Value.Model,
                    messages = conversation,
                    tools = toolList,
                    tool_choice = round < MaxRounds ? "auto" : "none",
                    temperature = 0.1,
                    max_completion_tokens = 2048,
                    reasoning_effort = "low",
                    stream = false,
                });

                using var response = await http.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("AI provider answered {Status}", (int)response.StatusCode);
                    return Error.InvalidState("The AI Brain is not available right now.");
                }

                using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                var message = json.RootElement.GetProperty("choices")[0].GetProperty("message");
                if (!message.TryGetProperty("tool_calls", out var calls) || calls.ValueKind != JsonValueKind.Array || calls.GetArrayLength() == 0)
                {
                    var text = message.TryGetProperty("content", out var content) ? content.GetString() : null;
                    return string.IsNullOrWhiteSpace(text) ? Error.InvalidState("The AI Brain gave no answer.") : Result<string>.Success(text);
                }

                // Keep the model's own request for tools in the conversation, then add each tool's answer.
                conversation.Add(new { role = "assistant", content = (string?)null, tool_calls = calls.Clone() });
                foreach (var call in calls.EnumerateArray())
                {
                    var function = call.GetProperty("function");
                    var output = await runTool(function.GetProperty("name").GetString() ?? string.Empty, function.GetProperty("arguments").GetString() ?? "{}", cancellationToken);
                    conversation.Add(new { role = "tool", tool_call_id = call.GetProperty("id").GetString(), content = output });
                }
            }

            return Error.InvalidState("The AI Brain gave no answer.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning("AI provider call failed: {Type}", ex.GetType().Name);
            return Error.InvalidState("The AI Brain is not available right now.");
        }
    }
}
