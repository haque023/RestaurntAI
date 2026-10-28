using RestaurantAI.Reservation.Domain;
using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Reservation.Tests;

public sealed class ReservationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private static Domain.Reservation Create(int partySize = 4, DateTimeOffset? startsAt = null) => Domain.Reservation.Create(
        Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), partySize, startsAt ?? Now.AddDays(1), null, null, Now);

    [Fact]
    public void Create_confirms_and_applies_default_duration()
    {
        var reservation = Create();

        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Equal(Domain.Reservation.DefaultDuration, reservation.EndsAt - reservation.StartsAt);
        Assert.IsType<ReservationCreated>(Assert.Single(reservation.DomainEvents));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void Create_rejects_invalid_party_sizes(int partySize) =>
        Assert.Throws<DomainException>(() => Create(partySize));

    [Fact]
    public void Create_rejects_times_in_the_past() =>
        Assert.Throws<DomainException>(() => Create(startsAt: Now.AddMinutes(-1)));

    [Fact]
    public void Cancel_is_not_allowed_after_start()
    {
        var reservation = Create(startsAt: Now.AddHours(1));

        Assert.Throws<DomainException>(() => reservation.Cancel("late", Now.AddHours(2)));
    }

    [Fact]
    public void Cancel_twice_is_rejected()
    {
        var reservation = Create();
        reservation.Cancel("plans changed", Now);

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Throws<DomainException>(() => reservation.Cancel("again", Now));
    }
}
