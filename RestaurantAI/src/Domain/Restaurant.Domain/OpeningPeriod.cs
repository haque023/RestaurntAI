namespace RestaurantAI.Restaurant.Domain;

/// <summary>
/// A weekly opening period in the restaurant's local time. A period whose closing time is
/// earlier than its opening time runs past midnight (e.g. Fri 18:00–02:00).
/// </summary>
public sealed record OpeningPeriod(DayOfWeek Day, TimeOnly Opens, TimeOnly Closes)
{
    public bool IsOvernight => Closes < Opens;
}

public static class OpeningHoursCalculator
{
    /// <summary>Whether any period is open at the given local date and time.</summary>
    public static bool IsOpen(IEnumerable<OpeningPeriod> periods, DateTime localTime)
    {
        var time = TimeOnly.FromDateTime(localTime);
        var today = localTime.DayOfWeek;
        var yesterday = (DayOfWeek)(((int)today + 6) % 7);

        foreach (var period in periods)
        {
            if (period.Day == today && (period.IsOvernight ? time >= period.Opens : time >= period.Opens && time < period.Closes))
            {
                return true;
            }

            // Tail of last night's overnight period.
            if (period.Day == yesterday && period.IsOvernight && time < period.Closes)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Converts UTC to the restaurant's IANA time zone; falls back to UTC for unknown zones.</summary>
    public static DateTime ToLocal(DateTimeOffset utcNow, string timeZone)
    {
        try
        {
            return TimeZoneInfo.ConvertTime(utcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return utcNow.UtcDateTime;
        }
    }
}
