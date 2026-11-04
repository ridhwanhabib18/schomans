using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Approvals;
using SchoMans.Domain.Attendance;
using SchoMans.Domain.Audit;
using SchoMans.Domain.Common;
using SchoMans.Domain.Grading;

namespace SchoMans.Infrastructure.Persistence;

/// <summary>
/// Records every change to grades, attendance records and approvals (F-11) as <see cref="AuditLog"/> rows in the same save, so
/// a change and its trace commit or fail together. It must run before <see cref="BaseEntitySaveChangesInterceptor"/>, which turns
/// deletes into updates and sets the timestamps.
/// </summary>
public sealed class AuditSaveChangesInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
{
    private static readonly HashSet<Type> Audited = [typeof(Grade), typeof(AttendanceRecord), typeof(Request), typeof(Approval)];

    // Bookkeeping columns say nothing about the business change.
    private static readonly HashSet<string> Skipped =
        [nameof(BaseEntity.Id), nameof(BaseEntity.CreatedAt), nameof(BaseEntity.UpdatedAt), nameof(BaseEntity.IsDeleted), nameof(BaseEntity.RowVersion)];

    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Record(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Record(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Record(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && Audited.Contains(e.Entity.GetType()))
            .ToList();
        if (entries.Count == 0)
        {
            return;
        }

        Guid? userId = currentUser.IsAuthenticated ? currentUser.UserId : null;
        foreach (var entry in entries)
        {
            if (Describe(entry) is var (action, oldValues, newValues))
            {
                var id = (Guid)entry.Property(nameof(BaseEntity.Id)).CurrentValue!;
                context.Set<AuditLog>().Add(AuditLog.Create(userId, action, entry.Entity.GetType().Name, id, oldValues, newValues));
            }
        }
    }

    private static (AuditAction Action, string? Old, string? New)? Describe(EntityEntry entry)
    {
        var properties = entry.Properties.Where(p => !Skipped.Contains(p.Metadata.Name)).ToList();
        switch (entry.State)
        {
            case EntityState.Added:
                return (AuditAction.Created, null, Serialize(properties, p => p.CurrentValue));
            case EntityState.Deleted:
                return (AuditAction.Deleted, Serialize(properties, p => p.OriginalValue), null);
            default:
                // Only what really changed; a save that touches nothing of the business data (a bumped row version) is no change.
                var changed = properties.Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)).ToList();
                var deleted = entry.Property(nameof(BaseEntity.IsDeleted)) is { IsModified: true, CurrentValue: true };
                if (changed.Count == 0 && !deleted)
                {
                    return null;
                }

                return (deleted ? AuditAction.Deleted : AuditAction.Updated, Serialize(changed, p => p.OriginalValue), Serialize(changed, p => p.CurrentValue));
        }
    }

    private static string Serialize(IEnumerable<PropertyEntry> properties, Func<PropertyEntry, object?> value) =>
        JsonSerializer.Serialize(properties.ToDictionary(p => p.Metadata.Name, value), Json);
}
