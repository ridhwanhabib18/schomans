using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Approvals;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Requests;

/// <summary>Shared pieces of the request use cases: the chains, who is the approver of a step, who may see a request.</summary>
internal static class RequestSupport
{
    /// <summary>The roles of the chain of a type, in the order they decide.</summary>
    public static async Task<List<ApproverRole>> ChainAsync(IAppDbContext db, RequestType type, CancellationToken ct) =>
        await db.ApprovalChainSteps.AsNoTracking().Where(s => s.RequestType == type).OrderBy(s => s.StepOrder).Select(s => s.ApproverRole).ToListAsync(ct);

    public static async Task<Dictionary<RequestType, List<ApproverRole>>> ChainsAsync(IAppDbContext db, CancellationToken ct) =>
        (await db.ApprovalChainSteps.AsNoTracking().OrderBy(s => s.StepOrder).Select(s => new { s.RequestType, s.ApproverRole }).ToListAsync(ct))
            .GroupBy(s => s.RequestType)
            .ToDictionary(g => g.Key, g => g.Select(s => s.ApproverRole).ToList());

    public static ApproverRole? RoleAtStep(IReadOnlyList<ApproverRole> chain, int step) =>
        step >= 1 && step <= chain.Count ? chain[step - 1] : null;

    /// <summary>The homeroom teacher of the classroom of the student a request is about.</summary>
    public static Task<Guid?> HomeroomTeacherIdAsync(IAppDbContext db, Guid? studentId, CancellationToken ct) =>
        studentId is null
            ? Task.FromResult<Guid?>(null)
            : db.Students.AsNoTracking().Where(s => s.Id == studentId).Select(s => (Guid?)s.Classroom!.HomeroomTeacherId).FirstOrDefaultAsync(ct);

    /// <summary>True when the signed-in user is the one who has to decide at a step with this role.</summary>
    public static async Task<bool> IsApproverAsync(IAppDbContext db, ICurrentUser user, ApproverRole role, Guid? studentId, CancellationToken ct) =>
        role switch
        {
            ApproverRole.Admin => user.Role == UserRole.Admin,
            ApproverRole.Principal => user.Role == UserRole.Principal,
            ApproverRole.HomeroomTeacher => user.Role == UserRole.Teacher
                && await SchoolMembership.TeacherIdAsync(db, user, ct) is { } teacherId
                && teacherId == await HomeroomTeacherIdAsync(db, studentId, ct),
            _ => false,
        };

    /// <summary>The user accounts that have to decide at a step with this role.</summary>
    public static async Task<List<Guid>> ApproverUserIdsAsync(IAppDbContext db, ApproverRole role, Guid? studentId, CancellationToken ct)
    {
        switch (role)
        {
            case ApproverRole.Admin:
                return await db.Users.Where(u => u.Role == UserRole.Admin).Select(u => u.Id).ToListAsync(ct);
            case ApproverRole.Principal:
                return await db.Users.Where(u => u.Role == UserRole.Principal).Select(u => u.Id).ToListAsync(ct);
            default:
                var homeroom = await HomeroomTeacherIdAsync(db, studentId, ct);
                return homeroom is null
                    ? []
                    : await db.Teachers.Where(t => t.Id == homeroom).Select(t => t.UserId).ToListAsync(ct);
        }
    }

    /// <summary>The requester, the family of the student, the staff who oversee requests, and the homeroom teacher of the student.</summary>
    public static async Task<bool> CanViewAsync(IAppDbContext db, ICurrentUser user, Request request, CancellationToken ct)
    {
        if (request.RequesterId == user.UserId || user.Role is UserRole.Admin or UserRole.Principal)
        {
            return true;
        }

        if (request.StudentId is not { } studentId)
        {
            return false;
        }

        return user.Role switch
        {
            UserRole.Parent => await db.StudentParents.AnyAsync(p => p.ParentUserId == user.UserId && p.StudentId == studentId, ct),
            UserRole.Student => await SchoolMembership.StudentIdAsync(db, user, ct) == studentId,
            UserRole.Teacher => await IsApproverAsync(db, user, ApproverRole.HomeroomTeacher, studentId, ct),
            _ => false,
        };
    }

    public static async Task<RequestDetailDto> DetailAsync(IAppDbContext db, ICurrentUser user, Guid requestId, CancellationToken ct)
    {
        var request = await db.Requests.AsNoTracking()
            .Include(r => r.Approvals).ThenInclude(a => a.Approver)
            .Include(r => r.Requester)
            .Include(r => r.Student).ThenInclude(s => s!.User)
            .Include(r => r.Student).ThenInclude(s => s!.Classroom)
            .FirstAsync(r => r.Id == requestId, ct);

        var chain = await ChainAsync(db, request.Type, ct);
        var waitingFor = request.IsPending ? RoleAtStep(chain, request.CurrentStep) : null;
        var canDecide = waitingFor is { } role && await IsApproverAsync(db, user, role, request.StudentId, ct);
        var letter = await db.Letters.AsNoTracking().Where(l => l.RequestId == request.Id).Select(l => new LetterInfoDto(l.LetterNumber, l.IssuedAt)).FirstOrDefaultAsync(ct);

        return new RequestDetailDto(
            request.Id,
            request.Type,
            request.Status,
            request.Requester!.FullName,
            request.StudentId,
            request.Student?.User?.FullName,
            request.Student?.Classroom?.Name,
            request.StartDate,
            request.EndDate,
            request.Reason,
            request.CurrentStep,
            chain,
            waitingFor,
            request.Approvals
                .OrderBy(a => a.Step)
                .Select(a => new ApprovalDto(a.Step, RoleAtStep(chain, a.Step) ?? ApproverRole.Principal, a.Approver!.FullName, a.Decision, a.Notes, a.DecidedAt))
                .ToList(),
            letter,
            canDecide,
            request.IsPending && request.RequesterId == user.UserId,
            request.CreatedAt);
    }
}
