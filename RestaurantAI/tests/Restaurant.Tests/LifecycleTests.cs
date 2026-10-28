using RestaurantAI.Restaurant.Domain;
using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Restaurant.Tests;

public sealed class LifecycleTests
{
    private static Domain.Restaurant SelfService()
    {
        var restaurant = Domain.Restaurant.Register("Dhaka Bites", "BDT", "Asia/Dhaka", requiresVerification: true);
        restaurant.AddBranch("Banani", "Road 11");
        return restaurant;
    }

    [Fact]
    public void Self_service_sign_ups_wait_for_verification_before_listing()
    {
        var restaurant = SelfService();
        Assert.Equal(RestaurantStatus.PendingVerification, restaurant.Status);

        var error = Assert.Throws<DomainException>(restaurant.Activate);
        Assert.Equal("restaurant.pending_verification", error.Code);

        restaurant.Approve();
        restaurant.Activate();
        Assert.True(restaurant.IsListed);
    }

    [Fact]
    public void Suspension_unlists_and_blocks_listing_until_reinstated()
    {
        var restaurant = SelfService();
        restaurant.Approve();
        restaurant.Activate();

        restaurant.Suspend("Food safety complaint");
        Assert.False(restaurant.IsListed);
        Assert.Equal("Food safety complaint", restaurant.SuspensionReason);
        Assert.Throws<DomainException>(restaurant.Activate);

        restaurant.Reinstate();
        Assert.Equal(RestaurantStatus.Draft, restaurant.Status);
        Assert.Null(restaurant.SuspensionReason);
    }

    [Fact]
    public void Only_pending_restaurants_can_be_approved() =>
        Assert.Throws<DomainException>(Domain.Restaurant.Register("Spice Route", "USD", "UTC").Approve);

    [Fact]
    public void Plan_limits_branches_and_cannot_be_downgraded_below_usage()
    {
        var restaurant = SelfService();
        var error = Assert.Throws<DomainException>(() => restaurant.AddBranch("Gulshan", "x"));
        Assert.Equal("restaurant.branch_limit", error.Code);

        restaurant.ChangePlan("growth");
        restaurant.AddBranch("Gulshan", "x");
        Assert.Equal(2, restaurant.Branches.Count);

        Assert.Throws<DomainException>(() => restaurant.ChangePlan("starter"));
        Assert.Throws<DomainException>(() => restaurant.ChangePlan("platinum"));
    }

    [Fact]
    public void Ai_knowledge_is_bounded()
    {
        var restaurant = SelfService();
        restaurant.UpdateAiSettings("  Cash and cards.  ", " warm ");
        Assert.Equal("Cash and cards.", restaurant.AiKnowledge);
        Assert.Equal("warm", restaurant.AiTone);

        Assert.Throws<DomainException>(() => restaurant.UpdateAiSettings(new string('x', Domain.Restaurant.MaxAiKnowledgeLength + 1), ""));
    }

    [Theory]
    [InlineData("", 4)]
    [InlineData("T1", 0)]
    [InlineData("T1", 51)]
    public void Tables_need_a_label_and_sensible_capacity(string label, int capacity) =>
        Assert.Throws<DomainException>(() => DiningTable.Create(Guid.NewGuid(), Guid.NewGuid(), label, capacity, "token-123456", DateTimeOffset.UtcNow));

    [Fact]
    public void Rotating_a_table_qr_replaces_the_token()
    {
        var table = DiningTable.Create(Guid.NewGuid(), Guid.NewGuid(), " T1 ", 4, "old-token-123", DateTimeOffset.UtcNow);
        table.RotateQr("new-token-456");
        Assert.Equal("T1", table.Label);
        Assert.Equal("new-token-456", table.QrToken);
    }
}
