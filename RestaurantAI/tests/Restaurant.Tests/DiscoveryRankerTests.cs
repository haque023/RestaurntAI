using RestaurantAI.Restaurant.Application.Discovery;
using RestaurantAI.Restaurant.Domain;

namespace RestaurantAI.Restaurant.Tests;

public sealed class DiscoveryRankerTests
{
    // Gulshan and Dhanmondi, Dhaka (~6.5 km apart); Chittagong ~215 km away.
    private static readonly (double Lat, double Lng) Gulshan = (23.7925, 90.4078);
    private static readonly (double Lat, double Lng) Dhanmondi = (23.7465, 90.3760);
    private static readonly (double Lat, double Lng) Chittagong = (22.3569, 91.7832);

    private static readonly DateTimeOffset MondayNoonDhaka = new(2026, 10, 19, 6, 0, 0, TimeSpan.Zero);
    private static readonly OpeningPeriod[] MondayLunch = [new(DayOfWeek.Monday, new TimeOnly(11, 0), new TimeOnly(15, 0))];

    private static DiscoveryListing Listing(string name, (double Lat, double Lng)? at, IReadOnlyList<OpeningPeriod>? hours = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), name, "", [], [], "Main", "", at?.Lat, at?.Lng, "Asia/Dhaka", hours ?? []);

    [Fact]
    public void Near_me_keeps_only_branches_within_the_radius_nearest_first()
    {
        var results = DiscoveryRanker.Apply(
            [Listing("Far", Chittagong), Listing("Close", Gulshan), Listing("Nearby", Dhanmondi), Listing("No location", null)],
            new DiscoveryQuery { Latitude = Gulshan.Lat, Longitude = Gulshan.Lng, RadiusKm = 10 },
            MondayNoonDhaka);

        Assert.Equal(["Close", "Nearby"], results.Select(r => r.RestaurantName));
        Assert.Equal(0, results[0].DistanceKm);
        Assert.InRange(results[1].DistanceKm!.Value, 5, 8);
    }

    [Fact]
    public void Open_now_uses_each_restaurant_time_zone()
    {
        var results = DiscoveryRanker.Apply(
            [Listing("Lunch place", null, MondayLunch), Listing("Closed today", null)],
            new DiscoveryQuery { OpenNow = true },
            MondayNoonDhaka);

        var open = Assert.Single(results);
        Assert.Equal("Lunch place", open.RestaurantName);
        Assert.True(open.IsOpenNow);
    }

    [Fact]
    public void Without_location_open_restaurants_come_first_then_by_name()
    {
        var results = DiscoveryRanker.Apply(
            [Listing("Bravo", null), Listing("Charlie", null, MondayLunch), Listing("Alpha", null)],
            new DiscoveryQuery(),
            MondayNoonDhaka);

        Assert.Equal(["Charlie", "Alpha", "Bravo"], results.Select(r => r.RestaurantName));
    }

    [Fact]
    public void Limit_is_applied_after_filtering()
    {
        var many = Enumerable.Range(0, 10).Select(i => Listing($"R{i}", Gulshan)).ToList();

        var results = DiscoveryRanker.Apply(many, new DiscoveryQuery { Latitude = Gulshan.Lat, Longitude = Gulshan.Lng, Limit = 3 }, MondayNoonDhaka);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Haversine_distance_is_accurate() =>
        Assert.InRange(GeoDistance.Kilometres(Gulshan.Lat, Gulshan.Lng, Chittagong.Lat, Chittagong.Lng), 200, 230);
}
