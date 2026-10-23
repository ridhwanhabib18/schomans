namespace SchoMans.Domain.Common;

/// <summary>Shared columns of every entity: soft delete, timestamps, optimistic concurrency.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsDeleted { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public void MarkDeleted() => IsDeleted = true;
}
