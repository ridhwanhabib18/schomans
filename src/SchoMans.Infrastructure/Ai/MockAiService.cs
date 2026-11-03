using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;

namespace SchoMans.Infrastructure.Ai;

/// <summary>
/// The default provider (ADR-004): fixed, deterministic answers, so demos and tests need no key and cost nothing. Each feature
/// adds its own answer here, shaped like what the feature expects from a real model.
/// </summary>
public sealed class MockAiService : IAiService
{
    public string Provider => AiProviders.Mock;

    public Task<Result<AiCompletion>> CompleteAsync(AiRequest request, CancellationToken ct)
    {
        var text = request.Feature switch
        {
            _ => request.JsonOutput ? "{\"mock\":true}" : "This is an answer of the mock AI.",
        };

        // The mock "reads" the prompt so its token counts move with the input, like a real provider's would.
        var tokens = (request.SystemPrompt.Length + request.UserPrompt.Length + text.Length) / 4;
        return Task.FromResult<Result<AiCompletion>>(new AiCompletion(text, Provider, "mock", tokens, 0m));
    }
}
