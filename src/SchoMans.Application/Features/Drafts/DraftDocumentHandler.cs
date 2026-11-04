using FluentValidation;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;

namespace SchoMans.Application.Features.Drafts;

public sealed record DraftDocumentCommand(DraftDocumentType Type, string Audience, string Notes);

public sealed class DraftDocumentValidator : AbstractValidator<DraftDocumentCommand>
{
    public const int AudienceMaxLength = 100;
    public const int NotesMaxLength = 1_500;

    public DraftDocumentValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Audience).NotEmpty().MaximumLength(AudienceMaxLength);
        RuleFor(x => x.Notes).NotEmpty().MaximumLength(NotesMaxLength);
    }
}

/// <summary>
/// Staff give the kind of document, the audience and short notes; the AI drafts the title and the text (AI-09). Nothing is saved,
/// published or sent: the draft goes into an editor where the staff member changes it. It counts as one of the person's daily AI calls.
/// </summary>
public sealed class DraftDocumentHandler(ICurrentUser currentUser, AiGateway gateway)
{
    public async Task<Result<DocumentDraftDto>> Handle(DraftDocumentCommand command, CancellationToken ct)
    {
        var completion = await gateway.RunAsync(currentUser.UserId, DraftPrompt.Build(command.Type, command.Audience.Trim(), command.Notes.Trim()), ct);
        return completion.IsSuccess ? DraftPrompt.Parse(completion.Value.Text) : completion.Error;
    }
}
