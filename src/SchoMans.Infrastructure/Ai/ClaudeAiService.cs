using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;

namespace SchoMans.Infrastructure.Ai;

/// <summary>
/// The Claude Messages API through a plain <see cref="HttpClient"/> (ADR-004, no SDK package). The system prompt is marked for
/// prompt caching; effort is low because every feature here is a short, structured draft. Costs come from the configured prices.
/// </summary>
public sealed class ClaudeAiService(HttpClient http, IOptions<AiOptions> options, ILogger<ClaudeAiService> logger) : IAiService
{
    private const string ApiVersion = "2023-06-01";

    private readonly ClaudeOptions _options = options.Value.Claude;

    public string Provider => AiProviders.Claude;

    public async Task<Result<AiCompletion>> CompleteAsync(AiRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Error.Unavailable("ai.not_configured", "The Claude API key is not set (Ai:Claude:ApiKey).");
        }

        var system = request.JsonOutput
            ? request.SystemPrompt + "\n\nAnswer with one JSON object and nothing else: no explanation and no code fence."
            : request.SystemPrompt;
        var body = new
        {
            model = _options.Model,
            max_tokens = _options.MaxTokens,
            system = new[] { new { type = "text", text = system, cache_control = new { type = "ephemeral" } } },
            messages = new[] { new { role = "user", content = request.UserPrompt } },
            output_config = new { effort = "low" },
        };

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/v1/messages") { Content = JsonContent.Create(body) };
            message.Headers.Add("x-api-key", _options.ApiKey);
            message.Headers.Add("anthropic-version", ApiVersion);

            using var response = await http.SendAsync(message, ct);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = document.RootElement;
            if (root.TryGetProperty("stop_reason", out var stop) && stop.GetString() == "refusal")
            {
                return Error.Unavailable("ai.refused", "The AI model declined this request.");
            }

            var text = new StringBuilder();
            foreach (var block in root.GetProperty("content").EnumerateArray())
            {
                if (block.GetProperty("type").GetString() == "text")
                {
                    text.Append(block.GetProperty("text").GetString());
                }
            }

            var usage = root.GetProperty("usage");
            var input = Count(usage, "input_tokens") + Count(usage, "cache_creation_input_tokens") + Count(usage, "cache_read_input_tokens");
            var output = Count(usage, "output_tokens");
            var cost = (input * _options.InputUsdPerMillion + output * _options.OutputUsdPerMillion) / 1_000_000m;
            return new AiCompletion(text.ToString(), Provider, _options.Model, input + output, cost);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogError(exception, "The Claude call for {Feature} failed", request.Feature);
            return Error.Unavailable("ai.provider_failed", "The AI model did not answer. Try again in a moment.");
        }
    }

    public async Task<Result<AiChatReply>> ChatAsync(AiChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Error.Unavailable("ai.not_configured", "The Claude API key is not set (Ai:Claude:ApiKey).");
        }

        var messages = new List<object>();
        foreach (var turn in request.Turns)
        {
            switch (turn.Role)
            {
                case "user":
                    messages.Add(new { role = "user", content = turn.Text });
                    break;
                case "assistant":
                    var blocks = new List<object>();
                    if (!string.IsNullOrEmpty(turn.Text))
                    {
                        blocks.Add(new { type = "text", text = turn.Text });
                    }

                    foreach (var call in turn.ToolCalls ?? [])
                    {
                        blocks.Add(new { type = "tool_use", id = call.Id, name = call.Name, input = JsonSerializer.Deserialize<JsonElement>(call.Arguments) });
                    }

                    messages.Add(new { role = "assistant", content = blocks });
                    break;
                case "tool":
                    messages.Add(new { role = "user", content = (turn.ToolResults ?? []).Select(r => new { type = "tool_result", tool_use_id = r.CallId, content = r.Content }).ToList() });
                    break;
            }
        }

        var body = new
        {
            model = _options.Model,
            max_tokens = _options.MaxTokens,
            system = new[] { new { type = "text", text = request.SystemPrompt, cache_control = new { type = "ephemeral" } } },
            tools = request.Tools.Select(t => new { name = t.Name, description = t.Description, input_schema = JsonSerializer.Deserialize<JsonElement>(t.InputSchema) }).ToList(),
            messages,
            output_config = new { effort = "low" },
        };

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/v1/messages") { Content = JsonContent.Create(body) };
            message.Headers.Add("x-api-key", _options.ApiKey);
            message.Headers.Add("anthropic-version", ApiVersion);

            using var response = await http.SendAsync(message, ct);
            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = document.RootElement;
            if (root.TryGetProperty("stop_reason", out var stop) && stop.GetString() == "refusal")
            {
                return Error.Unavailable("ai.refused", "The AI model declined this request.");
            }

            var text = new StringBuilder();
            var calls = new List<AiToolCall>();
            foreach (var block in root.GetProperty("content").EnumerateArray())
            {
                switch (block.GetProperty("type").GetString())
                {
                    case "text":
                        text.Append(block.GetProperty("text").GetString());
                        break;
                    case "tool_use":
                        calls.Add(new AiToolCall(block.GetProperty("id").GetString() ?? string.Empty, block.GetProperty("name").GetString() ?? string.Empty, block.GetProperty("input").GetRawText()));
                        break;
                }
            }

            var usage = root.GetProperty("usage");
            var input = Count(usage, "input_tokens") + Count(usage, "cache_creation_input_tokens") + Count(usage, "cache_read_input_tokens");
            var output = Count(usage, "output_tokens");
            var cost = (input * _options.InputUsdPerMillion + output * _options.OutputUsdPerMillion) / 1_000_000m;
            return new AiChatReply(text.ToString(), calls, Provider, _options.Model, input + output, cost);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogError(exception, "The Claude chat call for {Feature} failed", request.Feature);
            return Error.Unavailable("ai.provider_failed", "The AI model did not answer. Try again in a moment.");
        }
    }

    private static int Count(JsonElement usage, string name) => usage.TryGetProperty(name, out var value) && value.TryGetInt32(out var count) ? count : 0;
}
