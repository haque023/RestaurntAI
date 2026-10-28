using RestaurantAI.Restaurant.Domain;

namespace RestaurantAI.Restaurant.Tests;

public sealed class OpeningHoursTests
{
    private static readonly OpeningPeriod MondayLunch = new(DayOfWeek.Monday, new TimeOnly(11, 0), new TimeOnly(15, 0));
    private static readonly OpeningPeriod FridayLate = new(DayOfWeek.Friday, new TimeOnly(18, 0), new TimeOnly(2, 0));

    // 2026-10-19 is a Monday.
    private static DateTime Monday(int hour, int minute = 0) => new(2026, 10, 19, hour, minute, 0);

    [Theory]
    [InlineData(11, 0, true)]
    [InlineData(14, 59, true)]
    [InlineData(15, 0, false)]
    [InlineData(10, 59, false)]
    public void Same_day_period(int hour, int minute, bool open) =>
        Assert.Equal(open, OpeningHoursCalculator.IsOpen([MondayLunch], Monday(hour, minute)));

    [Fact]
    public void Overnight_period_is_open_late_on_its_own_day() =>
        Assert.True(OpeningHoursCalculator.IsOpen([FridayLate], new DateTime(2026, 10, 23, 23, 30, 0)));

    [Fact]
    public void Overnight_period_is_open_after_midnight_the_next_day() =>
        Assert.True(OpeningHoursCalculator.IsOpen([FridayLate], new DateTime(2026, 10, 24, 1, 30, 0)));

    [Fact]
    public void Overnight_period_closes_at_its_closing_time() =>
        Assert.False(OpeningHoursCalculator.IsOpen([FridayLate], new DateTime(2026, 10, 24, 2, 0, 0)));

    [Fact]
    public void No_hours_means_closed() =>
        Assert.False(OpeningHoursCalculator.IsOpen([], Monday(12)));

    [Fact]
    public void Converts_utc_to_the_restaurant_time_zone()
    {
        var local = OpeningHoursCalculator.ToLocal(new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.Zero), "Asia/Dhaka");
        Assert.Equal(12, local.Hour);
    }

    [Fact]
    public void Unknown_time_zones_fall_back_to_utc()
    {
        var local = OpeningHoursCalculator.ToLocal(new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.Zero), "Not/AZone");
        Assert.Equal(6, local.Hour);
    }
}
