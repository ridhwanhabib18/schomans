using SchoMans.Domain.Common;

namespace SchoMans.Domain.Approvals;

/// <summary>One step of the approval chain of a request type. The steps of a type are numbered 1, 2, 3 and so on, and run in that order.</summary>
public sealed class ApprovalChainStep : BaseEntity
{
    private ApprovalChainStep()
    {
    }

    public RequestType RequestType { get; private set; }

    public int StepOrder { get; private set; }

    public ApproverRole ApproverRole { get; private set; }

    public static ApprovalChainStep Create(RequestType requestType, int stepOrder, ApproverRole approverRole)
    {
        if (stepOrder < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(stepOrder), "Steps are numbered from 1.");
        }

        return new ApprovalChainStep { RequestType = requestType, StepOrder = stepOrder, ApproverRole = approverRole };
    }

    /// <summary>A chain is valid when it has at least one step and each role asked is possible for the type: only a request about a student has a homeroom teacher.</summary>
    public static bool IsValidChain(RequestType type, IReadOnlyList<ApproverRole> roles) =>
        roles.Count > 0
        && roles.Count <= 5
        && (type != RequestType.TeacherLeave || !roles.Contains(ApproverRole.HomeroomTeacher));
}
