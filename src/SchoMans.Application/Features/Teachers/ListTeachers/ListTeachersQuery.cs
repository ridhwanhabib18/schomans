namespace SchoMans.Application.Features.Teachers.ListTeachers;

public sealed record ListTeachersQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Sort = null, bool Desc = false)
{
    public static readonly string[] SortFields = ["name", "nip"];
}
