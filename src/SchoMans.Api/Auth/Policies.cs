namespace SchoMans.Api.Auth;

/// <summary>Authorization policy names. Each endpoint group names the policy it needs; the policies are registered in <see cref="AuthExtensions"/>.</summary>
public static class Policies
{
    public const string Principal = nameof(Principal);
    public const string Teacher = nameof(Teacher);
    public const string Student = nameof(Student);
    public const string Parent = nameof(Parent);
    public const string Admin = nameof(Admin);
    public const string Applicant = nameof(Applicant);

    /// <summary>Any signed-in person that holds one of the six SchoMans roles.</summary>
    public const string AnyRole = nameof(AnyRole);

    /// <summary>The people who write news and announcements: admin staff and the principal.</summary>
    public const string Editors = nameof(Editors);

    /// <summary>Staff who may read master data: admin, principal and teachers (a teacher only sees their own classes, enforced in Application).</summary>
    public const string StaffRead = nameof(StaffRead);

    /// <summary>The people who give or oversee assignments: teachers and the principal.</summary>
    public const string TeachingStaff = nameof(TeachingStaff);

    /// <summary>The people who make or follow requests: everybody inside the school (not applicants).</summary>
    public const string RequestUsers = nameof(RequestUsers);

    /// <summary>The people who can be an approver in a chain: admin staff, the principal and teachers (a homeroom teacher).</summary>
    public const string Approvers = nameof(Approvers);
}
