using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Assignments;

namespace SchoMans.Application.Features.QuestionBank;

// ---- save ----

public sealed record SaveQuestionsCommand(Guid SubjectId, List<QuestionInput> Questions);

public sealed class SaveQuestionsValidator : AbstractValidator<SaveQuestionsCommand>
{
    public const int MaxPerRequest = 30;

    public SaveQuestionsValidator()
    {
        RuleFor(x => x.Questions).Cascade(CascadeMode.Stop).NotNull().Must(q => q is { Count: >= 1 and <= MaxPerRequest });
        RuleForEach(x => x.Questions).Must(q => QuestionRules.Check(q) is null).WithMessage((_, q) => QuestionRules.Check(q) ?? "The question is not valid.");
    }
}

/// <summary>A teacher saves questions (written by hand or drafted by the AI and edited) into the bank of a subject they teach.</summary>
public sealed class SaveQuestionsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<List<QuestionDto>>> Handle(SaveQuestionsCommand command, CancellationToken ct)
    {
        var teacher = await QuestionRules.TeacherOfSubjectAsync(db, currentUser, command.SubjectId, ct);
        if (!teacher.IsSuccess)
        {
            return teacher.Error;
        }

        var created = command.Questions.Select(q => QuestionRules.ToEntity(command.SubjectId, teacher.Value, q)).ToList();
        db.Questions.AddRange(created);
        await db.SaveChangesAsync(ct);
        return created.Select(q => ToDto(q, currentUser.FullName, isMine: true)).ToList();
    }

    internal static QuestionDto ToDto(Question q, string createdByName, bool isMine) =>
        new(q.Id, q.SubjectId, q.Type, q.Difficulty, q.Body, [.. q.Options], q.AnswerKey, q.Explanation, createdByName, isMine);
}

// ---- list ----

public sealed record ListQuestionsQuery(Guid SubjectId, QuestionType? Type = null, QuestionDifficulty? Difficulty = null, string? Search = null, int Page = 1, int PageSize = 20);

public sealed class ListQuestionsValidator : AbstractValidator<ListQuestionsQuery>
{
    public ListQuestionsValidator()
    {
        this.RuleForPaging(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Type).IsInEnum().When(x => x.Type is not null);
        RuleFor(x => x.Difficulty).IsInEnum().When(x => x.Difficulty is not null);
        RuleFor(x => x.Search).MaximumLength(100);
    }
}

/// <summary>The bank of a subject: every question of it, for the teachers who teach it; the author's own are marked.</summary>
public sealed class ListQuestionsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<QuestionDto>>> Handle(ListQuestionsQuery query, CancellationToken ct)
    {
        var teacher = await QuestionRules.TeacherOfSubjectAsync(db, currentUser, query.SubjectId, ct);
        if (!teacher.IsSuccess)
        {
            return teacher.Error;
        }

        var questions = db.Questions.AsNoTracking().Where(q => q.SubjectId == query.SubjectId);
        if (query.Type is { } type)
        {
            questions = questions.Where(q => q.Type == type);
        }

        if (query.Difficulty is { } difficulty)
        {
            questions = questions.Where(q => q.Difficulty == difficulty);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            questions = questions.Where(q => q.Body.Contains(search));
        }

        var page = await questions
            .OrderByDescending(q => q.CreatedAt).ThenBy(q => q.Id)
            .Select(q => new { Question = q, Author = q.CreatedByTeacher!.User!.FullName })
            .ToPagedResultAsync(query.Page, query.PageSize, ct);

        return new PagedResult<QuestionDto>(
            page.Items.Select(r => SaveQuestionsHandler.ToDto(r.Question, r.Author, r.Question.CreatedByTeacherId == teacher.Value)).ToList(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }
}

// ---- change ----

public sealed record UpdateQuestionCommand(Guid Id, QuestionInput Question);

public sealed class UpdateQuestionValidator : AbstractValidator<UpdateQuestionCommand>
{
    public UpdateQuestionValidator() =>
        RuleFor(x => x.Question).Must(q => QuestionRules.Check(q) is null).WithMessage(x => QuestionRules.Check(x.Question) ?? "The question is not valid.");
}

/// <summary>Only the teacher who wrote a question changes it.</summary>
public sealed class UpdateQuestionHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<QuestionDto>> Handle(UpdateQuestionCommand command, CancellationToken ct)
    {
        var question = await db.Questions.FirstOrDefaultAsync(q => q.Id == command.Id, ct);
        var own = await OwnAsync(db, currentUser, question, ct);
        if (!own.IsSuccess)
        {
            return own.Error;
        }

        var q = command.Question;
        question!.Update(q.Type, q.Difficulty, q.Body, q.Options ?? [], q.AnswerKey, q.Explanation ?? string.Empty);
        await db.SaveChangesAsync(ct);
        return SaveQuestionsHandler.ToDto(question, currentUser.FullName, isMine: true);
    }

    internal static async Task<Result<Guid>> OwnAsync(IAppDbContext db, ICurrentUser user, Question? question, CancellationToken ct)
    {
        if (question is null)
        {
            return Error.NotFound("questions.not_found", "The question does not exist.");
        }

        var teacherId = await SchoolMembership.TeacherIdAsync(db, user, ct);
        return teacherId == question.CreatedByTeacherId
            ? teacherId.Value
            : Error.Forbidden("questions.not_yours", "Only the teacher who wrote a question can change it.");
    }
}

public sealed record DeleteQuestionCommand(Guid Id);

/// <summary>Deletes a question of the bank. Quizzes already built from it keep their own copy of the question, so they are not affected.</summary>
public sealed class DeleteQuestionHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<Unit>> Handle(DeleteQuestionCommand command, CancellationToken ct)
    {
        var question = await db.Questions.FirstOrDefaultAsync(q => q.Id == command.Id, ct);
        var own = await UpdateQuestionHandler.OwnAsync(db, currentUser, question, ct);
        if (!own.IsSuccess)
        {
            return own.Error;
        }

        db.Questions.Remove(question!);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}
