using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Approvals;
using SchoMans.Domain.Communication;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Requests.CreateRequest;

/// <param name="StudentId">The child for a leave or a letter; empty for a teacher's leave.</param>
/// <param name="StartDate">First day of a leave; empty for a letter.</param>
public sealed record CreateRequestCommand(RequestType Type, Guid? StudentId, DateOnly? StartDate, DateOnly? EndDate, string Reason);

public sealed class CreateRequestValidator : AbstractValidator<CreateRequestCommand>
{
    public const int MaxLeaveDays = 60;

    public CreateRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(Request.ReasonMaxLength);
        RuleFor(x => x.StudentId).NotEmpty().When(x => x.Type != RequestType.TeacherLeave).WithMessage("Choose the student.");
        RuleFor(x => x.StudentId).Null().When(x => x.Type == RequestType.TeacherLeave).WithMessage("A teacher's leave is not about a student.");

        When(x => x.Type != RequestType.CertificateLetter, () =>
        {
            RuleFor(x => x.StartDate).NotNull().WithMessage("Give the first day.");
            RuleFor(x => x.EndDate).NotNull().WithMessage("Give the last day.");
            RuleFor(x => x)
                .Must(x => x.StartDate is null || x.EndDate is null || x.EndDate >= x.StartDate)
                .WithName("EndDate")
                .WithMessage("The last day is not before the first day.");
            RuleFor(x => x)
                .Must(x => x.StartDate is null || x.EndDate is null || x.EndDate.Value.DayNumber - x.StartDate.Value.DayNumber < MaxLeaveDays)
                .WithName("EndDate")
                .WithMessage($"A leave is at most {MaxLeaveDays} days.");
        });
    }
}

/// <summary>
/// A parent asks for a student's leave or a letter about their child, a student asks for a letter about themselves, a
/// teacher asks for leave. The request starts at step 1 of the chain of its type and the approvers of that step are told.
/// </summary>
public sealed class CreateRequestHandler(IAppDbContext db, ICurrentUser currentUser, INotificationService notifications, ISchoolClock clock)
{
    public async Task<Result<RequestDetailDto>> Handle(CreateRequestCommand command, CancellationToken ct)
    {
        var allowed = command.Type switch
        {
            RequestType.StudentLeave => currentUser.Role == UserRole.Parent,
            RequestType.TeacherLeave => currentUser.Role == UserRole.Teacher,
            RequestType.CertificateLetter => currentUser.Role is UserRole.Parent or UserRole.Student,
            _ => false,
        };
        if (!allowed)
        {
            return Error.Forbidden("requests.not_allowed", "Your role cannot make this kind of request.");
        }

        if (command.StudentId is { } studentId && !await IsOwnStudentAsync(studentId, ct))
        {
            return Error.Forbidden("requests.not_your_student", "You can only make requests about yourself or your own child.");
        }

        var today = clock.Today;
        if (command.Type != RequestType.CertificateLetter && command.StartDate < today.AddDays(-7))
        {
            return Error.Validation("requests.too_early", "A leave cannot start more than a week in the past.");
        }

        var chain = await RequestSupport.ChainAsync(db, command.Type, ct);
        if (chain.Count == 0)
        {
            return Error.Conflict("requests.no_chain", "There is no approval chain for this kind of request yet.");
        }

        var request = Request.Create(command.Type, currentUser.UserId, command.StudentId, command.StartDate, command.EndDate, command.Reason);
        db.Requests.Add(request);
        await db.SaveChangesAsync(ct);

        await NotifyApproversAsync(request, chain[0], ct);
        return await RequestSupport.DetailAsync(db, currentUser, request.Id, ct);
    }

    private async Task<bool> IsOwnStudentAsync(Guid studentId, CancellationToken ct) =>
        currentUser.Role == UserRole.Student
            ? await SchoolMembership.StudentIdAsync(db, currentUser, ct) == studentId
            : await db.StudentParents.AnyAsync(p => p.ParentUserId == currentUser.UserId && p.StudentId == studentId, ct);

    private async Task NotifyApproversAsync(Request request, ApproverRole role, CancellationToken ct) =>
        await NotificationSender.SendAsync(
            db,
            notifications,
            await RequestSupport.ApproverUserIdsAsync(db, role, request.StudentId, ct),
            NotificationType.RequestAwaitingApproval,
            "A request waits for your decision",
            $"{currentUser.FullName}: {Shorten(request.Reason)}",
            $"/requests/{request.Id}",
            ct);

    private static string Shorten(string text) => text.Length <= 120 ? text : string.Concat(text.AsSpan(0, 119), "…");
}
