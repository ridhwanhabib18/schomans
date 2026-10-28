namespace SchoMans.Application.Features.Attendance;

/// <summary>Configuration section <c>Attendance</c>.</summary>
public sealed class AttendanceOptions
{
    public const string SectionName = "Attendance";

    public GeofenceOptions Geofence { get; set; } = new();

    /// <summary>A session still open this long after its lesson ended is closed by the system.</summary>
    public int CloseAfterLessonMinutes { get; set; } = 15;

    /// <summary>A session opened late stays open at least this long before the system may close it.</summary>
    public int MinimumOpenMinutes { get; set; } = 30;
}

/// <summary>The optional location check: a scan only counts within <see cref="RadiusMeters"/> of the school.</summary>
public sealed class GeofenceOptions
{
    public bool Enabled { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double RadiusMeters { get; set; } = 200;
}
