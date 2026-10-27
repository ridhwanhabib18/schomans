using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.Students.ListStudents;

/// <param name="Search">Matches the NISN, the name or the email.</param>
/// <param name="Sort">One of <see cref="ListStudentsQuery.SortFields"/>; defaults to name.</param>
public sealed record ListStudentsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? ClassroomId = null,
    StudentStatus? Status = null,
    string? Sort = null,
    bool Desc = false)
{
    public static readonly string[] SortFields = ["name", "nisn", "classroom", "dateOfBirth"];
}
