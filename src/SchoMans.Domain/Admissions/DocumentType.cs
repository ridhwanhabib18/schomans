namespace SchoMans.Domain.Admissions;

public enum DocumentType
{
    BirthCertificate,
    FamilyCard,
    ReportCard,
}

public enum DocumentStatus
{
    Pending,
    Verified,
    Rejected,
}
