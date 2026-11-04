namespace SchoMans.Application.Features.PrincipalAssistant.AskAssistant;

/// <param name="Question">What the principal asks, in plain language.</param>
/// <param name="History">The earlier turns of this conversation, kept by the browser (the server stores none); at most <see cref="AskAssistantValidator.MaxHistory"/>.</param>
public sealed record AskAssistantCommand(string Question, List<AssistantTurnDto>? History = null);
