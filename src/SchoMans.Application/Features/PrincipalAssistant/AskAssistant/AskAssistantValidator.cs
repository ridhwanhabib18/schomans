using FluentValidation;

namespace SchoMans.Application.Features.PrincipalAssistant.AskAssistant;

public sealed class AskAssistantValidator : AbstractValidator<AskAssistantCommand>
{
    public const int QuestionMaxLength = 500;
    public const int TurnMaxLength = 3000;
    public const int MaxHistory = 10;

    public AskAssistantValidator()
    {
        RuleFor(x => x.Question).NotEmpty().MaximumLength(QuestionMaxLength);
        RuleFor(x => x.History).Must(h => h is null || h.Count <= MaxHistory).WithMessage($"At most {MaxHistory} earlier turns are accepted.");
        RuleForEach(x => x.History).ChildRules(turn =>
        {
            turn.RuleFor(t => t.Role).Must(r => r is "user" or "assistant").WithMessage("A turn is from the user or the assistant.");
            turn.RuleFor(t => t.Text).NotEmpty().MaximumLength(TurnMaxLength);
        });
    }
}
