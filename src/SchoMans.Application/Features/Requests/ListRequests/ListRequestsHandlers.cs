using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Approvals;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Requests.ListRequests;

public sealed record ListMyRequestsQuery(RequestStatus? Status = null, int Page = 1, int PageSize = 20);

/// <summary>The requests that wait for the signed-in approver's decision.</summary>
public sealed record ListInboxQuery(int Page = 1, int PageSize = 20);

/// <summary>Every request, for the staff who oversee them (admin and principal).</summary>
public sealed record ListAllRequestsQuery(RequestStatus? Status = null, RequestType? Type = null, int Page = 1, int PageSize = 20);

public sealed class ListMyRequestsValidator : AbstractValidator<ListMyRequestsQuery>
{
    public ListMyRequestsValidator()
    {
        this.RuleForPaging(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class ListInboxValidator : AbstractValidator<ListInboxQuery>
{
    public ListInboxValidator() => this.RuleForPaging(x => x.Page, x => x.PageSize);
}

public sealed class ListAllRequestsValidator : AbstractValidator<ListAllRequestsQuery>
{
    public ListAllRequestsValidator()
    {
        this.RuleForPaging(x => x.Page, x => x.PageSize);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Type).IsInEnum();
    }
}

internal static class RequestListing
{
    public static IQueryable<RequestRow> Rows(IQueryable<Request> requests) =>
        requests.Select(r => new RequestRow(
            r.Id, r.Type, r.Status, r.Requester!.FullName, r.Student == null ? null : r.Student.User!.FullName, r.StartDate, r.EndDate, r.Reason, r.CurrentStep, r.CreatedAt));

    public static RequestListItemDto ToDto(RequestRow row, Dictionary<RequestType, List<ApproverRole>> chains)
    {
        var chain = chains.GetValueOrDefault(row.Type) ?? [];
        return new RequestListItemDto(
            row.Id,
            row.Type,
            row.Status,
            row.RequesterName,
            row.StudentName,
            row.StartDate,
            row.EndDate,
            row.Reason,
            row.CurrentStep,
            chain.Count,
            row.Status == RequestStatus.Pending ? RequestSupport.RoleAtStep(chain, row.CurrentStep) : null,
            row.CreatedAt);
    }
}

internal sealed record RequestRow(
    Guid Id,
    RequestType Type,
    RequestStatus Status,
    string RequesterName,
    string? StudentName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Reason,
    int CurrentStep,
    DateTimeOffset CreatedAt);

public sealed class ListMyRequestsHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<RequestListItemDto>>> Handle(ListMyRequestsQuery query, CancellationToken ct)
    {
        // What a person made, and for a parent or a student also what was made about their child or themselves.
        List<Guid> children = currentUser.Role switch
        {
            UserRole.Parent => await SchoolMembership.ChildIdsAsync(db, currentUser, ct),
            UserRole.Student => await SchoolMembership.StudentIdAsync(db, currentUser, ct) is { } own ? [own] : [],
            _ => [],
        };

        var requests = db.Requests.AsNoTracking()
            .Where(r => r.RequesterId == currentUser.UserId || (r.StudentId != null && children.Contains(r.StudentId.Value)));
        if (query.Status is { } status)
        {
            requests = requests.Where(r => r.Status == status);
        }

        var page = await RequestListing.Rows(requests.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)).ToPagedResultAsync(query.Page, query.PageSize, ct);
        var chains = await RequestSupport.ChainsAsync(db, ct);
        return new PagedResult<RequestListItemDto>(page.Items.Select(r => RequestListing.ToDto(r, chains)).ToList(), page.Page, page.PageSize, page.TotalCount);
    }
}

public sealed class ListInboxHandler(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<PagedResult<RequestListItemDto>>> Handle(ListInboxQuery query, CancellationToken ct)
    {
        var role = currentUser.Role switch
        {
            UserRole.Admin => ApproverRole.Admin,
            UserRole.Principal => ApproverRole.Principal,
            UserRole.Teacher => ApproverRole.HomeroomTeacher,
            _ => (ApproverRole?)null,
        };
        if (role is null)
        {
            return Error.Forbidden("requests.no_inbox", "Your role does not decide requests.");
        }

        var chains = await RequestSupport.ChainsAsync(db, ct);

        // The types whose chain has this role somewhere; the exact step is checked below, with the chain in memory.
        var types = chains.Where(c => c.Value.Contains(role.Value)).Select(c => c.Key).ToList();
        var pending = db.Requests.AsNoTracking().Where(r => r.Status == RequestStatus.Pending && types.Contains(r.Type));

        if (role == ApproverRole.HomeroomTeacher)
        {
            var teacherId = await SchoolMembership.TeacherIdAsync(db, currentUser, ct);
            pending = pending.Where(r => r.Student != null && r.Student.Classroom!.HomeroomTeacherId == teacherId);
        }

        var rows = await RequestListing.Rows(pending.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)).ToListAsync(ct);
        var mine = rows.Where(r => RequestSupport.RoleAtStep(chains[r.Type], r.CurrentStep) == role).ToList();

        var items = mine.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(r => RequestListing.ToDto(r, chains)).ToList();
        return new PagedResult<RequestListItemDto>(items, query.Page, query.PageSize, mine.Count);
    }
}

public sealed class ListAllRequestsHandler(IAppDbContext db)
{
    public async Task<Result<PagedResult<RequestListItemDto>>> Handle(ListAllRequestsQuery query, CancellationToken ct)
    {
        var requests = db.Requests.AsNoTracking().AsQueryable();
        if (query.Status is { } status)
        {
            requests = requests.Where(r => r.Status == status);
        }

        if (query.Type is { } type)
        {
            requests = requests.Where(r => r.Type == type);
        }

        var page = await RequestListing.Rows(requests.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)).ToPagedResultAsync(query.Page, query.PageSize, ct);
        var chains = await RequestSupport.ChainsAsync(db, ct);
        return new PagedResult<RequestListItemDto>(page.Items.Select(r => RequestListing.ToDto(r, chains)).ToList(), page.Page, page.PageSize, page.TotalCount);
    }
}
