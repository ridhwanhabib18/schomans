using SchoMans.Domain.Approvals;

namespace SchoMans.Application.Features.Requests;

public sealed record RequestListItemDto(
    Guid Id,
    RequestType Type,
    RequestStatus Status,
    string RequesterName,
    string? StudentName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Reason,
    int CurrentStep,
    int TotalSteps,
    ApproverRole? WaitingFor,
    DateTimeOffset CreatedAt);

public sealed record ApprovalDto(int Step, ApproverRole Role, string ApproverName, ApprovalDecision Decision, string? Notes, DateTimeOffset DecidedAt);

public sealed record LetterInfoDto(string LetterNumber, DateTimeOffset IssuedAt);

/// <param name="Chain">The roles of the chain, in the order they decide.</param>
/// <param name="WaitingFor">The role whose decision is awaited; null once the request is closed.</param>
/// <param name="CanDecide">The signed-in user is the one who has to decide now.</param>
/// <param name="CanCancel">The signed-in user is the requester and the request is still pending.</param>
public sealed record RequestDetailDto(
    Guid Id,
    RequestType Type,
    RequestStatus Status,
    string RequesterName,
    Guid? StudentId,
    string? StudentName,
    string? ClassroomName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Reason,
    int CurrentStep,
    IReadOnlyList<ApproverRole> Chain,
    ApproverRole? WaitingFor,
    IReadOnlyList<ApprovalDto> Approvals,
    LetterInfoDto? Letter,
    bool CanDecide,
    bool CanCancel,
    DateTimeOffset CreatedAt);

public sealed record ApprovalChainDto(RequestType Type, IReadOnlyList<ApproverRole> Roles, bool HasPendingRequests);
