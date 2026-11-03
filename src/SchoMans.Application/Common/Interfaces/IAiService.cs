using SchoMans.Application.Common.Errors;

namespace SchoMans.Application.Common.Interfaces;

/// <summary>What is asked of the model. The system prompt is the long, stable part (cacheable); the user prompt changes per call.</summary>
/// <param name="Feature">The feature asking (see <c>AiFeatures</c>); the mock answers per feature.</param>
/// <param name="JsonOutput">The answer must be a single JSON object. The caller still validates it before use.</param>
public sealed record AiRequest(string Feature, string SystemPrompt, string UserPrompt, bool JsonOutput = false);

public sealed record AiCompletion(string Text, string Provider, string Model, int Tokens, decimal CostUsd);

/// <summary>A function the model may ask to have called (tool use). <paramref name="InputSchema"/> is a JSON Schema object as text.</summary>
public sealed record AiTool(string Name, string Description, string InputSchema);

/// <param name="Id">Ties the call to its result; made up when the provider gives none.</param>
/// <param name="Arguments">The arguments as a JSON object.</param>
public sealed record AiToolCall(string Id, string Name, string Arguments);

public sealed record AiToolResult(string CallId, string Name, string Content);

/// <summary>One turn of a conversation: <c>user</c> text, <c>assistant</c> text with the tool calls it made, or <c>tool</c> with the results of those calls.</summary>
public sealed record AiChatTurn(string Role, string Text = "", IReadOnlyList<AiToolCall>? ToolCalls = null, IReadOnlyList<AiToolResult>? ToolResults = null);

public sealed record AiChatRequest(string Feature, string SystemPrompt, IReadOnlyList<AiChatTurn> Turns, IReadOnlyList<AiTool> Tools);

/// <summary>The model's next step: its final text, or the tools it wants called first (then <see cref="ToolCalls"/> is not empty).</summary>
public sealed record AiChatReply(string Text, IReadOnlyList<AiToolCall> ToolCalls, string Provider, string Model, int Tokens, decimal CostUsd);

/// <summary>
/// The one door to a model (ADR-004): the mock, a local Ollama or the Claude API. An answer is always a draft; the use case
/// that asked validates it and a person reviews it. A provider that fails or refuses gives an <c>Unavailable</c> error.
/// </summary>
public interface IAiService
{
    /// <summary>The provider behind this service, as configured (<c>Mock</c>, <c>Ollama</c>, <c>Claude</c>).</summary>
    string Provider { get; }

    Task<Result<AiCompletion>> CompleteAsync(AiRequest request, CancellationToken ct);

    /// <summary>One step of a conversation with tools: the caller runs the tool calls of the reply and asks again with their results.</summary>
    Task<Result<AiChatReply>> ChatAsync(AiChatRequest request, CancellationToken ct);
}
