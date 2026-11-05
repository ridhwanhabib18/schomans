using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Ai;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.QuestionBank;

/// <param name="File">The PDF. Only its text is read on the server and sent to the AI.</param>
public sealed record GenerateQuestionsCommand(Guid SubjectId, int Count, QuestionDifficulty Difficulty, QuestionType Type, Stream File, string FileName, long Length);

/// <param name="Questions">Drafts for the teacher to edit; nothing is saved.</param>
/// <param name="Truncated">The PDF had more text than the AI is given; the questions come from the first part.</param>
public sealed record GeneratedQuestionsDto(List<QuestionInput> Questions, int Pages, bool Truncated);

public sealed class GenerateQuestionsValidator : AbstractValidator<GenerateQuestionsCommand>
{
    public const long MaxBytes = 10 * 1024 * 1024;
    public const int MaxPages = 30;

    public GenerateQuestionsValidator()
    {
        RuleFor(x => x.Count).InclusiveBetween(1, QuestionPrompt.MaxCount);
        RuleFor(x => x.Difficulty).IsInEnum();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Length).GreaterThan(0).LessThanOrEqualTo(MaxBytes).WithMessage("The PDF must be at most 10 MB.");
        RuleFor(x => x.FileName).NotEmpty().Must(n => n.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).WithMessage("Upload a PDF file.");
    }
}

/// <summary>
/// AI-06: a teacher uploads a PDF (30 pages at most) and asks for questions of a type and a difficulty. The text is read here, only the
/// text goes to the AI, and the answer is checked rule by rule before it comes back as drafts. It uses one of the teacher's daily AI calls.
/// </summary>
public sealed class GenerateQuestionsHandler(
    IAppDbContext db, ICurrentUser currentUser, IPdfTextReader pdf, AiGateway gateway, IValidator<GenerateQuestionsCommand> validator)
{
    public async Task<Result<GeneratedQuestionsDto>> Handle(GenerateQuestionsCommand command, CancellationToken ct)
    {
        var check = await validator.ValidateAsync(command, ct);
        if (!check.IsValid)
        {
            return Error.Validation("questions.invalid_request", string.Join(" ", check.Errors.Select(e => e.ErrorMessage)));
        }

        var teacher = await QuestionRules.TeacherOfSubjectAsync(db, currentUser, command.SubjectId, ct);
        if (!teacher.IsSuccess)
        {
            return teacher.Error;
        }

        // PdfPig needs a seekable stream, and the file is small (10 MB at most).
        using var buffer = new MemoryStream();
        await command.File.CopyToAsync(buffer, ct);
        if (buffer.Length < 5 || buffer.GetBuffer()[0] != '%' || buffer.GetBuffer()[1] != 'P' || buffer.GetBuffer()[2] != 'D' || buffer.GetBuffer()[3] != 'F')
        {
            return Error.Validation("questions.pdf_invalid", "The file is not a PDF that can be read.");
        }

        buffer.Position = 0;
        var text = pdf.Read(buffer, GenerateQuestionsValidator.MaxPages);
        if (!text.IsSuccess)
        {
            return text.Error;
        }

        var truncated = text.Value.Text.Length > QuestionPrompt.MaxMaterialLength;
        var material = truncated ? text.Value.Text[..QuestionPrompt.MaxMaterialLength] : text.Value.Text;
        var subject = await db.Subjects.AsNoTracking().Where(s => s.Id == command.SubjectId).Select(s => s.Name).FirstAsync(ct);

        var completion = await gateway.RunAsync(currentUser.UserId, QuestionPrompt.Build(subject, command.Type, command.Difficulty, command.Count, material), ct);
        if (!completion.IsSuccess)
        {
            return completion.Error;
        }

        var parsed = QuestionPrompt.Parse(completion.Value.Text, command.Type, command.Difficulty, command.Count);
        return parsed.IsSuccess ? new GeneratedQuestionsDto(parsed.Value, text.Value.Pages, truncated) : parsed.Error;
    }
}
