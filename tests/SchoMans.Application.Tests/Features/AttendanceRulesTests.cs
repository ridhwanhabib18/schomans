using SchoMans.Application.Features.Attendance;
using SchoMans.Domain.Attendance;

namespace SchoMans.Application.Tests.Features;

public class AttendanceRulesTests
{
    [Fact]
    public void The_distance_between_two_points_is_measured_on_the_earth()
    {
        // Monas to Bundaran HI in Jakarta: about 2.2 km.
        var meters = Geofence.DistanceMeters(-6.1754, 106.8272, -6.1950, 106.8230);

        Assert.InRange(meters, 2_100, 2_300);
        Assert.Equal(0, Geofence.DistanceMeters(-6.2, 106.8, -6.2, 106.8), 3);
    }

    [Fact]
    public void A_phone_inside_the_radius_is_at_school()
    {
        var fence = new GeofenceOptions { Enabled = true, Latitude = -6.2, Longitude = 106.8, RadiusMeters = 200 };

        Assert.True(Geofence.IsInside(fence, -6.2, 106.8));
        Assert.True(Geofence.IsInside(fence, -6.2015, 106.8)); // about 167 m south
        Assert.False(Geofence.IsInside(fence, -6.203, 106.8)); // about 334 m south
    }

    [Fact]
    public void Counts_add_up_and_the_present_share_is_rounded_to_one_decimal()
    {
        var counts = AttendanceCountsDto.From(
            [AttendanceStatus.Present, AttendanceStatus.Present, AttendanceStatus.Sick, AttendanceStatus.Absent, AttendanceStatus.Excused, AttendanceStatus.Present]);

        Assert.Equal(new AttendanceCountsDto(3, 1, 1, 1, 6, 50.0m), counts);
        Assert.Equal(66.7m, AttendanceCountsDto.From([AttendanceStatus.Present, AttendanceStatus.Present, AttendanceStatus.Absent]).PresentPercent);
    }

    [Fact]
    public void No_lessons_means_zero_percent_not_a_division_by_zero()
    {
        Assert.Equal(new AttendanceCountsDto(0, 0, 0, 0, 0, 0), AttendanceCountsDto.From([]));
    }

    [Fact]
    public void Default_options_close_forgotten_sessions_a_quarter_hour_after_the_lesson()
    {
        var options = new AttendanceOptions();

        Assert.Equal(15, options.CloseAfterLessonMinutes);
        Assert.Equal(30, options.MinimumOpenMinutes);
        Assert.False(options.Geofence.Enabled);
    }
}
