using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;

namespace SchoMans.Infrastructure.Ai;

/// <summary>A local model served by Ollama (ADR-004): free, and nothing leaves the machine. Uses the chat endpoint without streaming.</summary>
public sealed class OllamaAiService(HttpClient http, IOptions<AiOptions> options, ILogger<OllamaAiService> logger) : IAiService
{
    private readonly OllamaOptions _options = options.Value.Ollama;

    public string Provider => AiProviders.Ollama;

    public async Task<Result<AiCompletion>> CompleteAsync(AiRequest request, CancellationToken ct)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = _options.Model,
            ["stream"] = false,
            ["messages"] = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt, images = (request.Images ?? []).Select(i => Convert.ToBase64String(i.Data)).ToList() },
            },
        };
        if (request.JsonOutput)
        {
            body["format"] = "json";
        }

        try
        {
            using var response = await http.PostAsJsonAsync($"{_options.BaseUrl.TrimEnd('/')}/api/chat", body, ct);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = document.RootElement;
            var text = root.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            var tokens = Count(root, "prompt_eval_count") + Count(root, "eval_count");
            return new AiCompletion(text, Provider, _options.Model, tokens, 0m);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogError(exception, "The Ollama call for {Feature} failed", request.Feature);
            return Error.Unavailable("ai.provider_failed", "The AI model did not answer. Check that Ollama runs and has the model, then try again.");
        }
    }

    public async Task<Result<AiChatReply>> ChatAsync(AiChatRequest request, CancellationToken ct)
    {
        var messages = new List<object> { new { role = "system", content = request.SystemPrompt } };
        foreach (var turn in request.Turns)
        {
            switch (turn.Role)
            {
                case "user":
                    messages.Add(new { role = "user", content = turn.Text });
                    break;
                case "assistant":
                    messages.Add(new
                    {
                        role = "assistant",
                        content = turn.Text,
                        tool_calls = (turn.ToolCalls ?? []).Select(c => new { function = new { name = c.Name, arguments = JsonSerializer.Deserialize<JsonElement>(c.Arguments) } }).ToList(),
                    });
                    break;
                case "tool":
                    messages.AddRange((turn.ToolResults ?? []).Select(r => (object)new { role = "tool", tool_name = r.Name, content = r.Content }));
                    break;
            }
        }

        var body = new
        {
            model = _options.Model,
            stream = false,
            messages,
            tools = request.Tools.Select(t => new { type = "function", function = new { name = t.Name, description = t.Description, parameters = JsonSerializer.Deserialize<JsonElement>(t.InputSchema) } }).ToList(),
        };

        try
        {
            using var response = await http.PostAsJsonAsync($"{_options.BaseUrl.TrimEnd('/')}/api/chat", body, ct);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = document.RootElement;
            var message = root.GetProperty("message");
            var calls = new List<AiToolCall>();
            if (message.TryGetProperty("tool_calls", out var toolCalls))
            {
                foreach (var call in toolCalls.EnumerateArray())
                {
                    var function = call.GetProperty("function");
                    // Ollama gives no call ids; the position is enough to tell the calls of one reply apart.
                    calls.Add(new AiToolCall($"call_{calls.Count + 1}", function.GetProperty("name").GetString() ?? string.Empty, function.TryGetProperty("arguments", out var arguments) ? arguments.GetRawText() : "{}"));
                }
            }

            var text = message.TryGetProperty("content", out var content) ? content.GetString() ?? string.Empty : string.Empty;
            return new AiChatReply(text, calls, Provider, _options.Model, Count(root, "prompt_eval_count") + Count(root, "eval_count"), 0m);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogError(exception, "The Ollama chat call for {Feature} failed", request.Feature);
            return Error.Unavailable("ai.provider_failed", "The AI model did not answer. Check that Ollama runs and has the model, then try again.");
        }
    }

    private static int Count(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var count) ? count : 0;
}
