using SchoMans.Application.Common.Errors;

namespace SchoMans.Application.Common.Interfaces;

/// <summary>What is asked of the model. The system prompt is the long, stable part (cacheable); the user prompt changes per call.</summary>
/// <param name="Feature">The feature asking (see <c>AiFeatures</c>); the mock answers per feature.</param>
/// <param name="JsonOutput">The answer must be a single JSON object. The caller still validates it before use.</param>
public sealed record AiRequest(string Feature, string SystemPrompt, string UserPrompt, bool JsonOutput = false);

public sealed record AiCompletion(string Text, string Provider, string Model, int Tokens, decimal CostUsd);

/// <summary>
/// The one door to a model (ADR-004): the mock, a local Ollama or the Claude API. An answer is always a draft; the use case
/// that asked validates it and a person reviews it. A provider that fails or refuses gives an <c>Unavailable</c> error.
/// </summary>
public interface IAiService
{
    /// <summary>The provider behind this service, as configured (<c>Mock</c>, <c>Ollama</c>, <c>Claude</c>).</summary>
    string Provider { get; }

    Task<Result<AiCompletion>> CompleteAsync(AiRequest request, CancellationToken ct);
}
