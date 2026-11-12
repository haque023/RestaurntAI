namespace RestaurantAI.Restaurant.Api.Models;

public static class GeoDistance
{
    private const double EarthRadiusKm = 6371.0088;

    /// <summary>Great-circle distance in kilometres (haversine).</summary>
    public static double Kilometres(double lat1, double lon1, double lat2, double lon2)
    {
        static double Radians(double degrees) => degrees * Math.PI / 180;

        var dLat = Radians(lat2 - lat1);
        var dLon = Radians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) + (Math.Cos(Radians(lat1)) * Math.Cos(Radians(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2));
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>Latitude/longitude bounds that contain every point within <paramref name="radiusKm"/>.</summary>
    public static (double MinLat, double MaxLat, double MinLon, double MaxLon) BoundingBox(double lat, double lon, double radiusKm)
    {
        var latDelta = radiusKm / 111.32;
        var cos = Math.Cos(lat * Math.PI / 180);
        var lonDelta = cos < 1e-6 ? 180 : Math.Min(180, radiusKm / (111.32 * cos));
        return (lat - latDelta, lat + latDelta, lon - lonDelta, lon + lonDelta);
    }
}
