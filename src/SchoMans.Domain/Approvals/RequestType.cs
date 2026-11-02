namespace SchoMans.Domain.Approvals;

public enum RequestType
{
    StudentLeave,
    TeacherLeave,
    CertificateLetter,
}

/// <summary>
/// Who decides at a step of the chain. The homeroom teacher is not a Keycloak role: it is the homeroom teacher of the
/// classroom of the student the request is about.
/// </summary>
public enum ApproverRole
{
    HomeroomTeacher,
    Admin,
    Principal,
}

public enum RequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
}

public enum ApprovalDecision
{
    Approved,
    Rejected,
}
