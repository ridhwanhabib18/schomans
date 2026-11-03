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
            ["messages"] = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt },
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

    private static int Count(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var count) ? count : 0;
}
