namespace SchoMans.Application.Features.AcademicYears.CreateAcademicYear;

/// <summary>Creates an inactive academic year. Make it the current one with the activate use case or the rollover.</summary>
public sealed record CreateAcademicYearCommand(string Name, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<TermInput> Terms);
