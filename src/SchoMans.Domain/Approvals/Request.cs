using SchoMans.Domain.Common;
using SchoMans.Domain.Students;

namespace SchoMans.Domain.Approvals;

/// <summary>
/// A request (student leave, teacher leave, certificate letter) that goes through the approval chain of its type, one
/// step at a time. <see cref="CurrentStep"/> is the number of the step that waits for a decision.
/// </summary>
public sealed class Request : BaseEntity
{
    public const int ReasonMaxLength = 1_000;
    public const int NoteMaxLength = 500;

    private Request()
    {
    }

    public RequestType Type { get; private set; }

    public Guid RequesterId { get; private set; }

    public User? Requester { get; private set; }

    /// <summary>The student a leave or a letter is about. Empty for a teacher's leave.</summary>
    public Guid? StudentId { get; private set; }

    public Student? Student { get; private set; }

    /// <summary>The days of a leave. A certificate letter has no days.</summary>
    public DateOnly? StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public RequestStatus Status { get; private set; } = RequestStatus.Pending;

    public int CurrentStep { get; private set; } = 1;

    public DateTimeOffset? DecidedAt { get; private set; }

    public List<Approval> Approvals { get; private set; } = [];

    public static Request Create(RequestType type, Guid requesterId, Guid? studentId, DateOnly? startDate, DateOnly? endDate, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (reason.Trim().Length > ReasonMaxLength)
        {
            throw new ArgumentException($"A reason is at most {ReasonMaxLength} characters.", nameof(reason));
        }

        if (type == RequestType.TeacherLeave && studentId is not null)
        {
            throw new ArgumentException("A teacher's leave is not about a student.", nameof(studentId));
        }

        if (type != RequestType.TeacherLeave && studentId is null)
        {
            throw new ArgumentException("A student leave or a letter is about a student.", nameof(studentId));
        }

        if (type == RequestType.CertificateLetter)
        {
            startDate = endDate = null;
        }
        else if (startDate is null || endDate is null || endDate < startDate)
        {
            throw new ArgumentException("A leave needs a first day and a last day that is not before it.");
        }

        return new Request
        {
            Type = type,
            RequesterId = requesterId,
            StudentId = studentId,
            StartDate = startDate,
            EndDate = endDate,
            Reason = reason.Trim(),
        };
    }

    public bool IsPending => Status == RequestStatus.Pending;

    /// <summary>
    /// The current approver decides. An approval moves the request to the next step, or ends it as approved after the last
    /// one; a rejection ends it at once. Returns false when the request is not pending or the step is not the current one.
    /// </summary>
    public bool Decide(ApprovalDecision decision, Guid approverId, string? note, int totalSteps, DateTimeOffset now)
    {
        if (!IsPending || totalSteps < 1)
        {
            return false;
        }

        if (decision == ApprovalDecision.Rejected)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(note);
        }

        if (note is { Length: > NoteMaxLength })
        {
            throw new ArgumentException($"A note is at most {NoteMaxLength} characters.", nameof(note));
        }

        Approvals.Add(Approval.Create(Id, approverId, CurrentStep, decision, note, now));

        if (decision == ApprovalDecision.Rejected)
        {
            Status = RequestStatus.Rejected;
            DecidedAt = now;
        }
        else if (CurrentStep >= totalSteps)
        {
            Status = RequestStatus.Approved;
            DecidedAt = now;
        }
        else
        {
            CurrentStep++;
        }

        return true;
    }

    /// <summary>The requester withdraws a request that is still waiting.</summary>
    public bool Cancel(DateTimeOffset now)
    {
        if (!IsPending)
        {
            return false;
        }

        Status = RequestStatus.Cancelled;
        DecidedAt = now;
        return true;
    }
}
