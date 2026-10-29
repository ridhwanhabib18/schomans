using SchoMans.Domain.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Approvals;

/// <summary>One decision of an approver on a step of a request.</summary>
public sealed class Approval : BaseEntity
{
    private Approval()
    {
    }

    public Guid RequestId { get; private set; }

    public Guid ApproverId { get; private set; }

    public User? Approver { get; private set; }

    public int Step { get; private set; }

    public ApprovalDecision Decision { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset DecidedAt { get; private set; }

    public static Approval Create(Guid requestId, Guid approverId, int step, ApprovalDecision decision, string? notes, DateTimeOffset now) => new()
    {
        RequestId = requestId,
        ApproverId = approverId,
        Step = step,
        Decision = decision,
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        DecidedAt = now,
    };
}
