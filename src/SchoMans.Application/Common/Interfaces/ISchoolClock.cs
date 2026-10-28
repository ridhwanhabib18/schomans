namespace SchoMans.Application.Common.Interfaces;

/// <summary>
/// The time of the school. Timetables are local times (WIB for the demo school); the database stores instants in UTC.
/// This is the one place that converts between the two, so a lesson "today at 07:00" means the same everywhere.
/// </summary>
public interface ISchoolClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>The current local date of the school.</summary>
    DateOnly Today { get; }

    /// <summary>The current local time of day of the school.</summary>
    TimeOnly TimeOfDay { get; }

    /// <summary>The instant of a local date and time of the school.</summary>
    DateTimeOffset ToUtc(DateOnly date, TimeOnly time);

    /// <summary>The local date of the school at an instant.</summary>
    DateOnly ToSchoolDate(DateTimeOffset instant);

    /// <summary>The local date and time of the school at an instant.</summary>
    DateTime ToSchoolDateTime(DateTimeOffset instant);
}
