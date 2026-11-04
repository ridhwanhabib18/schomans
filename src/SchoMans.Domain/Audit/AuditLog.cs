using SchoMans.Domain.Common;

namespace SchoMans.Domain.Audit;

public enum AuditAction
{
    Created,
    Updated,
    Deleted,
}

/// <summary>
/// One recorded change to a grade, an attendance record or an approval (F-11): who, when, and the old and new values. Rows
/// are only ever added; the moment of the change is <see cref="BaseEntity.CreatedAt"/>.
/// </summary>
public sealed class AuditLog : BaseEntity
{
    public const int EntityNameMaxLength = 64;

    private AuditLog()
    {
    }

    /// <summary>The signed-in user behind the change; null when a background job made it.</summary>
    public Guid? UserId { get; private set; }

    public AuditAction Action { get; private set; }

    public string EntityName { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    /// <summary>JSON of the values before the change; null for a new row.</summary>
    public string? OldValues { get; private set; }

    /// <summary>JSON of the values after the change; null for a deleted row.</summary>
    public string? NewValues { get; private set; }

    public static AuditLog Create(Guid? userId, AuditAction action, string entityName, Guid entityId, string? oldValues, string? newValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        return new AuditLog { UserId = userId, Action = action, EntityName = entityName, EntityId = entityId, OldValues = oldValues, NewValues = newValues };
    }
}
