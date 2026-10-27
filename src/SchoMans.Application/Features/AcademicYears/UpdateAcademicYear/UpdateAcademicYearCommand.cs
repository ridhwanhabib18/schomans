namespace SchoMans.Application.Features.AcademicYears.UpdateAcademicYear;

/// <param name="Terms">Terms with an id are updated, terms without one are added. A term is never removed here.</param>
public sealed record UpdateAcademicYearCommand(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<TermInput> Terms,
    string RowVersion);
