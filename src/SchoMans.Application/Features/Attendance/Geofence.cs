namespace SchoMans.Application.Features.Attendance;

/// <summary>The distance check of the optional location rule.</summary>
public static class Geofence
{
    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>Great-circle distance between two points (haversine), in meters.</summary>
    public static double DistanceMeters(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        static double Radians(double degrees) => degrees * Math.PI / 180;

        var dLat = Radians(latitude2 - latitude1);
        var dLon = Radians(longitude2 - longitude1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
            + (Math.Cos(Radians(latitude1)) * Math.Cos(Radians(latitude2)) * Math.Pow(Math.Sin(dLon / 2), 2));
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public static bool IsInside(GeofenceOptions fence, double latitude, double longitude) =>
        DistanceMeters(fence.Latitude, fence.Longitude, latitude, longitude) <= fence.RadiusMeters;
}
