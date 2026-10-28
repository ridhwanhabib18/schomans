namespace SchoMans.Application.Features.Classrooms.ListClassrooms;

public sealed record ListClassroomsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? AcademicYearId = null,
    string? Sort = null,
    bool Desc = false)
{
    public static readonly string[] SortFields = ["name", "academicYear"];
}
