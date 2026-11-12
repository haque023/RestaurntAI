namespace RestaurantAI.Restaurant.Api.Models;

public sealed record TableRequest(string Label, int Capacity);

public sealed record TableStatusRequest(TableStatus Status);

public sealed record TableResponse(Guid Id, Guid BranchId, string Label, int Capacity, string Status, string QrToken)
{
    public static TableResponse From(DiningTable t) => new(t.Id, t.BranchId, t.Label, t.Capacity, t.Status.ToString(), t.QrToken);
}
