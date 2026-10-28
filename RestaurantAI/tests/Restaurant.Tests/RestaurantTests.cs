using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Restaurant.Tests;

public sealed class RestaurantTests
{
    [Fact]
    public void Cannot_be_listed_without_a_branch() =>
        Assert.Throws<DomainException>(() => Domain.Restaurant.Register("Spice Route", "USD", "UTC").Activate());

    [Fact]
    public void Listing_and_unlisting()
    {
        var restaurant = Domain.Restaurant.Register("Spice Route", "USD", "UTC");
        restaurant.AddBranch("Downtown", "1 Main St");

        restaurant.Activate();
        Assert.True(restaurant.IsListed);

        restaurant.Unlist();
        Assert.False(restaurant.IsListed);
    }

    [Fact]
    public void Profile_tags_are_normalized()
    {
        var restaurant = Domain.Restaurant.Register("Spice Route", "USD", "UTC");

        restaurant.UpdateProfile("  Bengali kitchen ", [" Bengali", "bengali", "Indian ", ""], ["Halal"]);

        Assert.Equal("Bengali kitchen", restaurant.Description);
        Assert.Equal(["bengali", "indian"], restaurant.Cuisines);
        Assert.Equal(["halal"], restaurant.DietaryOptions);
    }

    [Theory]
    [InlineData(91.0, 0.0)]
    [InlineData(0.0, 181.0)]
    public void Branch_rejects_invalid_coordinates(double lat, double lng)
    {
        var branch = Domain.Restaurant.Register("Spice Route", "USD", "UTC").AddBranch("Downtown", "1 Main St");
        Assert.Throws<DomainException>(() => branch.UpdateDetails("Downtown", "1 Main St", lat, lng));
    }

    [Fact]
    public void Branch_requires_both_coordinates()
    {
        var branch = Domain.Restaurant.Register("Spice Route", "USD", "UTC").AddBranch("Downtown", "1 Main St");
        Assert.Throws<DomainException>(() => branch.UpdateDetails("Downtown", "1 Main St", 23.8, null));
    }
}
