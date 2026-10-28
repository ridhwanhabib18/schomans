namespace SchoMans.Api.IntegrationTests.Infrastructure;

/// <summary>A clock the test moves by hand. The school clock of the API reads it, so "today" and "now" are what the test says.</summary>
public sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private static readonly TimeZoneInfo WibZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jakarta");

    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;

    public void Set(DateTimeOffset now) => _now = now;

    /// <summary>The instant of a WIB date and time, for building a test clock that starts at a lesson.</summary>
    public static DateTimeOffset Wib(DateOnly date, TimeOnly time) =>
        new DateTimeOffset(date.ToDateTime(time), WibZone.BaseUtcOffset).ToUniversalTime();

    /// <summary>Today in WIB by the real clock.</summary>
    public static DateOnly WibToday() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, WibZone).DateTime);
}
