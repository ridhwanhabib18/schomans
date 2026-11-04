using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Errors;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Audit;

namespace SchoMans.Application.Features.Audit.ListAuditLogs;

/// <param name="EntityName">Grade, AttendanceRecord, Request or Approval; empty for all.</param>
public sealed record ListAuditLogsQuery(
    string? EntityName = null, Guid? EntityId = null, Guid? UserId = null, DateOnly? From = null, DateOnly? To = null, int Page = 1, int PageSize = 20);

public sealed record AuditLogDto(
    Guid Id,
    DateTimeOffset At,
    Guid? UserId,
    string? UserName,
    string Action,
    string EntityName,
    Guid EntityId,
    string? OldValues,
    string? NewValues);

public sealed class ListAuditLogsValidator : AbstractValidator<ListAuditLogsQuery>
{
    public ListAuditLogsValidator()
    {
        this.RuleForPaging(x => x.Page, x => x.PageSize);
        RuleFor(x => x.EntityName).MaximumLength(AuditLog.EntityNameMaxLength);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null);
    }
}

/// <summary>The audit trail, newest first, for admin staff and the principal.</summary>
public sealed class ListAuditLogsHandler(IAppDbContext db, ISchoolClock clock)
{
    public async Task<Result<PagedResult<AuditLogDto>>> Handle(ListAuditLogsQuery query, CancellationToken ct)
    {
        var logs = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            logs = logs.Where(l => l.EntityName == query.EntityName);
        }

        if (query.EntityId is { } entityId)
        {
            logs = logs.Where(l => l.EntityId == entityId);
        }

        if (query.UserId is { } userId)
        {
            logs = logs.Where(l => l.UserId == userId);
        }

        if (query.From is { } from)
        {
            var start = clock.ToUtc(from, TimeOnly.MinValue);
            logs = logs.Where(l => l.CreatedAt >= start);
        }

        if (query.To is { } to)
        {
            var end = clock.ToUtc(to.AddDays(1), TimeOnly.MinValue);
            logs = logs.Where(l => l.CreatedAt < end);
        }

        return await logs
            .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
            .Select(l => new AuditLogDto(
                l.Id,
                l.CreatedAt,
                l.UserId,
                db.Users.Where(u => u.Id == l.UserId).Select(u => u.FullName).FirstOrDefault(),
                l.Action.ToString(),
                l.EntityName,
                l.EntityId,
                l.OldValues,
                l.NewValues))
            .ToPagedResultAsync(query.Page, query.PageSize, ct);
    }
}
