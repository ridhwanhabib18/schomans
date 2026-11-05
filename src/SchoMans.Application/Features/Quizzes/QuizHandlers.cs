using System.Globalization;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Application.Features.Assignments;
using SchoMans.Application.Features.Assignments.CreateAssignment;
using SchoMans.Application.Features.Grading;
using SchoMans.Domain.Assignments;
using SchoMans.Domain.Communication;

namespace SchoMans.Application.Features.Quizzes;

/// <summary>Builds what a viewer may see of a quiz and of one attempt at it.</summary>
internal static class QuizView
{
    public static async Task<List<QuizScoring.Row>> RowsAsync(IAppDbContext db, Guid assignmentId, CancellationToken ct)
    {
        var rows = await db.AssignmentQuestions.AsNoTracking()
            .Where(q => q.AssignmentId == assignmentId)
            .OrderBy(q => q.Order)
            .Select(q => new { q.QuestionId, q.Order, q.Points, q.Question!.Type, q.Question.Difficulty, q.Question.Body, q.Question.OptionsJson, q.Question.AnswerKey, q.Question.Explanation })
            .ToListAsync(ct);
        return rows
            .Select(r => new QuizScoring.Row(r.QuestionId, r.Order, r.Type, r.Difficulty, r.Body, JsonSerializer.Deserialize<List<string>>(r.OptionsJson) ?? [], r.AnswerKey, r.Explanation, r.Points))
            .ToList();
    }

    /// <param name="staff">The teacher of the quiz or the principal: sees the key, the explanations and the points at any time.</param>
    /// <param name="attemptOf">Whose attempt to show, or null for none.</param>
    public static async Task<QuizDto> BuildAsync(IAppDbContext db, Assignment quiz, bool staff, bool canEdit, Guid? attemptOf, DateTimeOffset now, CancellationToken ct)
    {
        var rows = await RowsAsync(db, quiz.Id, ct);
        var resultsVisible = now >= quiz.DueAt;
        var showDetail = staff || resultsVisible;

        var submission = attemptOf is null
            ? null
            : await db.Submissions.AsNoTracking().Where(s => s.AssignmentId == quiz.Id && s.StudentId == attemptOf && s.AnswersJson != null)
                .Select(s => new { s.Id, s.StudentId, StudentName = s.Student!.User!.FullName, s.SubmittedAt, s.AnswersJson, s.ShortPointsJson, s.Score, s.GradedAt })
                .FirstOrDefaultAsync(ct);

        var answers = QuizScoring.ReadAnswers(submission?.AnswersJson);
        var shortPoints = QuizScoring.ReadShortPoints(submission?.ShortPointsJson);

        var questions = rows.Select(q => new QuizQuestionDto(
            q.QuestionId,
            q.Order,
            q.Type,
            q.Difficulty,
            q.Body,
            q.Options,
            q.Points,
            showDetail ? q.AnswerKey : null,
            showDetail ? q.Explanation : null,
            submission is null ? null : answers.GetValueOrDefault(q.QuestionId),
            submission is not null && showDetail ? QuizScoring.PointsOf(q, answers, shortPoints) : null)).ToList();

        QuizAttemptDto? attempt = null;
        if (submission is not null)
        {
            var toGrade = QuizScoring.ShortAnswersToGrade(rows, shortPoints);
            var status = !showDetail ? QuizAttemptStatus.Submitted : submission.GradedAt is null ? QuizAttemptStatus.WaitingForTeacher : QuizAttemptStatus.Scored;
            attempt = new QuizAttemptDto(submission.Id, submission.StudentId, submission.StudentName, submission.SubmittedAt, status, showDetail ? submission.Score : null, showDetail ? toGrade : 0);
        }

        return new QuizDto(quiz.Id, quiz.Title, quiz.Description, quiz.DueAt, !resultsVisible, resultsVisible, canEdit, rows.Sum(r => r.Points), questions, attempt);
    }
}

// ---- create ----

public sealed class CreateQuizValidator : AbstractValidator<CreateQuizCommand>
{
    public CreateQuizValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(Assignment.TitleMaxLength);
        RuleFor(x => x.Description).MaximumLength(Assignment.DescriptionMaxLength);
        RuleFor(x => x.ClassroomIds).Cascade(CascadeMode.Stop).NotNull().Must(c => c.Count > 0);
        RuleFor(x => x.Questions).Cascade(CascadeMode.Stop).NotNull()
            .Must(q => q.Count is >= 1 and <= AssignmentQuestion.MaxQuestionsPerQuiz).WithMessage($"A quiz has 1 to {AssignmentQuestion.MaxQuestionsPerQuiz} questions.")
            .Must(q => q.Select(x => x.QuestionId).Distinct().Count() == q.Count).WithMessage("A question can be in a quiz only once.");
        RuleForEach(x => x.Questions).ChildRules(q => q.RuleFor(x => x.Points).InclusiveBetween(1, AssignmentQuestion.MaxPoints));
    }
}

/// <summary>
/// A teacher builds an online quiz from the question bank of a subject they teach, for classrooms they teach it in. The quiz is an
/// assignment of type Quiz (its scores count in the Quiz component), the students are notified, and it closes at the due date.
/// </summary>
public sealed class CreateQuizHandler(IAppDbContext db, ICurrentUser currentUser, INotificationService notifications, ISchoolClock clock)
{
    public async Task<Result<AssignmentDto>> Handle(CreateQuizCommand command, CancellationToken ct)
    {
        var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
        if (teacherId is null)
        {
            return Error.Forbidden("assignments.not_a_teacher", "Only teachers give assignments.");
        }

        var classroomIds = command.ClassroomIds.Distinct().ToList();
        var problem = await AssignmentRules.CheckTeachesAsync(db, teacherId.Value, command.SubjectId, classroomIds, ct);
        if (problem is not null)
        {
            return problem;
        }

        if (command.DueAt <= clock.UtcNow)
        {
            return Error.Validation("quizzes.due_in_past", "A quiz closes at its due date, so the due date must be in the future.");
        }

        var termId = await db.Terms.AsNoTracking().Where(t => t.IsActive && t.AcademicYear!.IsActive).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
        if (termId is null)
        {
            return Error.Conflict("assignments.no_active_term", "There is no current term. Ask the admin staff to set one.");
        }

        var wanted = command.Questions.Select(q => q.QuestionId).ToList();
        var found = await db.Questions.AsNoTracking().Where(q => wanted.Contains(q.Id)).Select(q => new { q.Id, q.SubjectId }).ToListAsync(ct);
        if (found.Count != wanted.Count)
        {
            return Error.NotFound("quizzes.question_not_found", "A question of the quiz does not exist.");
        }

        if (found.Any(q => q.SubjectId != command.SubjectId))
        {
            return Error.Validation("quizzes.question_other_subject", "Every question must be from the question bank of the subject of the quiz.");
        }

        var quiz = Assignment.CreateQuiz(teacherId.Value, command.SubjectId, termId.Value, command.Title, command.Description ?? string.Empty, command.DueAt, classroomIds);
        db.Assignments.Add(quiz);
        for (var i = 0; i < command.Questions.Count; i++)
        {
            db.AssignmentQuestions.Add(AssignmentQuestion.Create(quiz.Id, command.Questions[i].QuestionId, i + 1, command.Questions[i].Points));
        }

        await db.SaveChangesAsync(ct);

        var subjectName = await db.Subjects.Where(s => s.Id == command.SubjectId).Select(s => s.Name).FirstAsync(ct);
        await AssignmentNotices.NewAssignmentAsync(db, notifications, clock, quiz.Id, quiz.Title, subjectName, quiz.DueAt, classroomIds, ct);
        return (await AssignmentLoader.LoadAsync(db, quiz.Id, canEdit: true, studentId: null, ct))!;
    }
}

// ---- read ----

/// <summary>
/// A quiz as its viewer may see it: the questions without the answers while it is open for a student; the key, the explanations, the
/// points and the score after the due date; everything for the teacher of the quiz. The teacher and the principal name a student to open an attempt.
/// </summary>
public sealed class GetQuizHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock)
{
    public async Task<Result<QuizDto>> Handle(GetQuizQuery query, CancellationToken ct)
    {
        var quiz = await db.Assignments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == query.AssignmentId, ct);
        if (quiz is null || !quiz.IsQuiz)
        {
            return Error.NotFound("quizzes.not_found", "The quiz does not exist.");
        }

        var viewer = await AssignmentAccess.ForAsync(db, currentUser, quiz.Id, quiz.TeacherId, query.StudentId, ct);
        if (!viewer.CanView)
        {
            return Error.Forbidden("assignments.not_yours", "This assignment is not for you.");
        }

        var staff = viewer.StudentId is null;
        var attemptOf = staff ? query.StudentId : viewer.StudentId;
        return await QuizView.BuildAsync(db, quiz, staff, viewer.IsOwner, attemptOf, clock.UtcNow, ct);
    }
}

// ---- take ----

public sealed class SubmitQuizValidator : AbstractValidator<SubmitQuizCommand>
{
    public SubmitQuizValidator()
    {
        RuleFor(x => x.Answers).NotNull();
        RuleForEach(x => x.Answers).ChildRules(a => a.RuleFor(x => x.Answer).MaximumLength(Question.AnswerKeyMaxLength));
    }
}

/// <summary>
/// A student takes the quiz once, before the due date, with no time limit. Multiple choice is scored at once; a quiz with only multiple
/// choice is scored and enters the grade, one with short answers waits for the teacher. A student sees nothing of the result before the due date.
/// </summary>
public sealed class SubmitQuizHandler(IAppDbContext db, ICurrentUser currentUser, ISchoolClock clock)
{
    public async Task<Result<QuizDto>> Handle(SubmitQuizCommand command, CancellationToken ct)
    {
        var studentId = await SchoolMembership.StudentIdAsync(db, currentUser, ct);
        if (studentId is null || !await AssignmentAccess.GivenToStudentAsync(db, command.AssignmentId, studentId.Value, ct))
        {
            return Error.Forbidden("assignments.not_yours", "This assignment is not for you.");
        }

        var quiz = await db.Assignments.AsNoTracking().FirstAsync(a => a.Id == command.AssignmentId, ct);
        if (!quiz.IsQuiz)
        {
            return Error.Conflict("quizzes.not_a_quiz", "This assignment is not an online quiz.");
        }

        var now = clock.UtcNow;
        if (now >= quiz.DueAt)
        {
            return Error.Conflict("quizzes.closed", "The quiz is closed: its due date has passed.");
        }

        if (await db.Submissions.AnyAsync(s => s.AssignmentId == quiz.Id && s.StudentId == studentId, ct))
        {
            return Error.Conflict("quizzes.already_submitted", "You have already taken this quiz. It can be taken once.");
        }

        var rows = await QuizView.RowsAsync(db, quiz.Id, ct);
        var byId = rows.ToDictionary(r => r.QuestionId);
        if (command.Answers.Select(a => a.QuestionId).Distinct().Count() != command.Answers.Count || command.Answers.Any(a => !byId.ContainsKey(a.QuestionId)))
        {
            return Error.Validation("quizzes.unknown_question", "An answer is for a question that is not in this quiz.");
        }

        var answers = rows.ToDictionary(r => r.QuestionId, _ => (string?)null);
        foreach (var input in command.Answers)
        {
            var row = byId[input.QuestionId];
            var text = string.IsNullOrWhiteSpace(input.Answer) ? null : input.Answer.Trim();
            if (text is not null && row.Type == QuestionType.MultipleChoice && !(int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var position) && position < row.Options.Count))
            {
                return Error.Validation("quizzes.invalid_answer", "A multiple choice answer is the position of one of its options.");
            }

            answers[input.QuestionId] = text;
        }

        var submission = Submission.CreateQuiz(quiz.Id, studentId.Value, JsonSerializer.Serialize(answers), now);
        db.Submissions.Add(submission);

        // Only multiple choice: the quiz is scored right away and its score becomes a grade of the subject.
        if (QuizScoring.ShortAnswersToGrade(rows, new Dictionary<Guid, decimal>()) == 0)
        {
            if (await GradeBook.IsStudentLockedAsync(db, studentId.Value, quiz.TermId, ct))
            {
                return Error.Conflict("grades.published", "The grades of this term are published; the quiz can no longer be taken.");
            }

            submission.CompleteQuiz(QuizScoring.Score(rows, answers, new Dictionary<Guid, decimal>()), null, now);
            await GradeBook.SyncSubmissionGradeAsync(db, submission, quiz, ct);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Error.Conflict("quizzes.already_submitted", "You have already taken this quiz. It can be taken once.");
        }

        return await QuizView.BuildAsync(db, quiz, staff: false, canEdit: false, studentId, now, ct);
    }
}

// ---- grade the short answers ----

public sealed class GradeQuizValidator : AbstractValidator<GradeQuizCommand>
{
    public GradeQuizValidator()
    {
        RuleFor(x => x.Points).Cascade(CascadeMode.Stop).NotNull().Must(p => p.Count >= 1 && p.Select(x => x.QuestionId).Distinct().Count() == p.Count);
        RuleForEach(x => x.Points).ChildRules(p => p.RuleFor(x => x.Points).GreaterThanOrEqualTo(0).PrecisionScale(5, 2, false));
    }
}

/// <summary>
/// The teacher of the quiz gives points to the short answers of one attempt. When every short answer has points the quiz is scored: the
/// score is set, it becomes a grade of the subject and the student is notified. Points can be corrected later.
/// </summary>
public sealed class GradeQuizHandler(IAppDbContext db, ICurrentUser currentUser, INotificationService notifications, ISchoolClock clock)
{
    public async Task<Result<QuizDto>> Handle(GradeQuizCommand command, CancellationToken ct)
    {
        var quiz = await db.Assignments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == command.AssignmentId, ct);
        if (quiz is null || !quiz.IsQuiz)
        {
            return Error.NotFound("quizzes.not_found", "The quiz does not exist.");
        }

        if (await SchoolMembership.TeacherIdAsync(db, currentUser, ct) != quiz.TeacherId)
        {
            return Error.Forbidden("assignments.not_yours", "Only the teacher who gave the quiz can score it.");
        }

        var submission = await db.Submissions.FirstOrDefaultAsync(s => s.AssignmentId == quiz.Id && s.StudentId == command.StudentId && s.AnswersJson != null, ct);
        if (submission is null)
        {
            return Error.NotFound("quizzes.no_attempt", "This student has not taken the quiz.");
        }

        if (await GradeBook.IsStudentLockedAsync(db, submission.StudentId, quiz.TermId, ct))
        {
            return Error.Conflict("grades.published", "The grades of this term are published; unpublish them before changing a score.");
        }

        var rows = await QuizView.RowsAsync(db, quiz.Id, ct);
        var byId = rows.ToDictionary(r => r.QuestionId);
        foreach (var input in command.Points)
        {
            if (!byId.TryGetValue(input.QuestionId, out var row) || row.Type != QuestionType.ShortAnswer)
            {
                return Error.Validation("quizzes.not_a_short_answer", "Points are given to the short answer questions of the quiz only.");
            }

            if (input.Points > row.Points)
            {
                return Error.Validation("quizzes.points_out_of_range", "The points of a short answer are between 0 and what the question is worth.");
            }
        }

        var shortPoints = QuizScoring.ReadShortPoints(submission.ShortPointsJson);
        foreach (var input in command.Points)
        {
            shortPoints[input.QuestionId] = input.Points;
        }

        submission.SetShortPoints(JsonSerializer.Serialize(shortPoints));

        var firstTime = !submission.IsGraded;
        if (QuizScoring.ShortAnswersToGrade(rows, shortPoints) == 0)
        {
            var score = QuizScoring.Score(rows, QuizScoring.ReadAnswers(submission.AnswersJson), shortPoints);
            submission.CompleteQuiz(score, currentUser.UserId, clock.UtcNow);
            await GradeBook.SyncSubmissionGradeAsync(db, submission, quiz, ct);
        }

        await db.SaveChangesAsync(ct);

        if (submission.IsGraded)
        {
            var studentUserId = await db.Students.Where(s => s.Id == submission.StudentId).Select(s => s.UserId).FirstAsync(ct);
            await NotificationSender.SendAsync(
                db,
                notifications,
                [studentUserId],
                NotificationType.AssignmentGraded,
                firstTime ? $"Your quiz was scored: {quiz.Title}" : $"Your quiz score was changed: {quiz.Title}",
                // The score is not told before the due date: until then a student sees nothing of the result.
                clock.UtcNow >= quiz.DueAt ? string.Create(CultureInfo.InvariantCulture, $"Score {submission.Score:0.##}") : "The result is shown after the due date.",
                AssignmentNotices.LinkFor(quiz.Id),
                ct);
        }

        return await QuizView.BuildAsync(db, quiz, staff: true, canEdit: true, submission.StudentId, clock.UtcNow, ct);
    }
}
