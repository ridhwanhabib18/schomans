using SchoMans.Domain.Common;

namespace SchoMans.Domain.Students;

public sealed class AcademicYear : BaseEntity
{
    private AcademicYear()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public bool IsActive { get; private set; }

    public static AcademicYear Create(string name, DateOnly startDate, DateOnly endDate, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (endDate <= startDate)
        {
            throw new ArgumentException("An academic year must end after it starts.", nameof(endDate));
        }

        return new AcademicYear { Name = name.Trim(), StartDate = startDate, EndDate = endDate, IsActive = isActive };
    }
}
