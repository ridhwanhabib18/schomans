namespace SchoMans.Application.Features.Subjects.ListSubjects;

public sealed record ListSubjectsQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Sort = null, bool Desc = false)
{
    public static readonly string[] SortFields = ["name", "minimumPassingScore"];
}
