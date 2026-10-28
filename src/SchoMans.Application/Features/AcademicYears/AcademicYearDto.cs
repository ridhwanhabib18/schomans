using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SchoMans.Application.Common;
using SchoMans.Application.Common.Interfaces;
using SchoMans.Domain.Students;

namespace SchoMans.Application.Features.AcademicYears;

public sealed record TermDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, bool IsActive);

public sealed record AcademicYearDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive,
    IReadOnlyList<TermDto> Terms,
    int ClassroomCount,
    string RowVersion);

/// <param name="Id">Set for an existing term, empty for a new one.</param>
public sealed record TermInput(Guid? Id, string Name, DateOnly StartDate, DateOnly EndDate);

public sealed class TermInputValidator : AbstractValidator<TermInput>
{
    public TermInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("A term must end after it starts.");
    }
}

internal static class AcademicYearMapping
{
    public static bool CanRead(UserRole role) => role is UserRole.Admin or UserRole.Principal or UserRole.Teacher;

    public static async Task<List<AcademicYearDto>> LoadAsync(IAppDbContext db, IReadOnlyCollection<Guid> yearIds, CancellationToken ct)
    {
        var years = await db.AcademicYears.AsNoTracking().Where(y => yearIds.Contains(y.Id)).ToListAsync(ct);
        var terms = await db.Terms.AsNoTracking().Where(t => yearIds.Contains(t.AcademicYearId)).OrderBy(t => t.StartDate).ToListAsync(ct);
        var counts = await db.Classrooms.AsNoTracking()
            .Where(c => yearIds.Contains(c.AcademicYearId))
            .GroupBy(c => c.AcademicYearId)
            .Select(g => new { YearId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.YearId, x => x.Count, ct);

        return years
            .OrderByDescending(y => y.StartDate)
            .Select(y => new AcademicYearDto(
                y.Id,
                y.Name,
                y.StartDate,
                y.EndDate,
                y.IsActive,
                terms.Where(t => t.AcademicYearId == y.Id).Select(t => new TermDto(t.Id, t.Name, t.StartDate, t.EndDate, t.IsActive)).ToList(),
                counts.GetValueOrDefault(y.Id),
                RowVersionCodec.Encode(y.RowVersion)))
            .ToList();
    }

    /// <summary>The checks shared by create, update and rollover: dates, the number of terms, unique term names, terms inside the year.</summary>
    public static string? CheckYearShape(DateOnly start, DateOnly end, IReadOnlyList<TermInput> terms)
    {
        if (end <= start)
        {
            return "An academic year must end after it starts.";
        }

        if (terms.Count is < 1 or > 4)
        {
            return "An academic year has between 1 and 4 terms.";
        }

        if (terms.Select(t => t.Name.Trim().ToLowerInvariant()).Distinct().Count() != terms.Count)
        {
            return "The term names must be different from each other.";
        }

        if (terms.Any(t => t.StartDate < start || t.EndDate > end))
        {
            return "Every term must lie inside the academic year.";
        }

        return null;
    }
}
