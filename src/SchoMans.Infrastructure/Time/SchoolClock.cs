using Microsoft.Extensions.Options;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Infrastructure.Time;

/// <summary>Configuration section <c>School</c>.</summary>
public sealed class SchoolOptions
{
    public const string SectionName = "School";

    /// <summary>IANA time zone of the school, for example Asia/Jakarta (WIB).</summary>
    public string TimeZone { get; set; } = "Asia/Jakarta";

    /// <summary>Letterhead of the report cards. A fictional school: the demo uses no real school data.</summary>
    public string Name { get; set; } = "SchoMans Demo School";

    public string Address { get; set; } = "Jl. Pendidikan No. 1, Jakarta";

    public string Contact { get; set; } = "demo@schomans.example";
}

/// <summary>The school's local time from <see cref="TimeProvider"/> and the configured time zone.</summary>
public sealed class SchoolClock(TimeProvider timeProvider, IOptions<SchoolOptions> options) : ISchoolClock
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(ToSchoolDateTime(UtcNow));

    public TimeOnly TimeOfDay => TimeOnly.FromDateTime(ToSchoolDateTime(UtcNow));

    public TimeZoneInfo Zone => _zone;

    public DateTimeOffset ToUtc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, _zone.GetUtcOffset(local)).ToUniversalTime();
    }

    public DateOnly ToSchoolDate(DateTimeOffset instant) => DateOnly.FromDateTime(ToSchoolDateTime(instant));

    public DateTime ToSchoolDateTime(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, _zone).DateTime;
}
