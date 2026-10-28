using Microsoft.Extensions.Options;

namespace SchoMans.Application.Features.Attendance.GetScanSettings;

/// <summary>Tells the check-in page whether it must ask the phone for its location.</summary>
public sealed class GetScanSettingsHandler(IOptions<AttendanceOptions> options)
{
    public ScanSettingsDto Handle() => new(options.Value.Geofence.Enabled, options.Value.Geofence.RadiusMeters);
}
