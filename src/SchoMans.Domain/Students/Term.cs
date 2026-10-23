using SchoMans.Domain.Common;

namespace SchoMans.Domain.Students;

/// <summary>A semester within an academic year.</summary>
public sealed class Term : BaseEntity
{
    private Term()
    {
    }

    public Guid AcademicYearId { get; private set; }

    public AcademicYear? AcademicYear { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public bool IsActive { get; private set; }

    public static Term Create(Guid academicYearId, string name, DateOnly startDate, DateOnly endDate, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (endDate <= startDate)
        {
            throw new ArgumentException("A term must end after it starts.", nameof(endDate));
        }

        return new Term
        {
            AcademicYearId = academicYearId,
            Name = name.Trim(),
            StartDate = startDate,
            EndDate = endDate,
            IsActive = isActive,
        };
    }
}
