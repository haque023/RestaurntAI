using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Restaurant.Api.Models;

public enum TableStatus
{
    Available,
    Occupied,
    Reserved,
    OutOfService,
}

/// <summary>
/// A table in a branch. Its QR token is an opaque random value printed on the table; scanning
/// it opens the AI waiter with this restaurant, branch and table as context.
/// </summary>
public sealed class DiningTable : AggregateRoot<Guid>, IBranchScoped
{
    private DiningTable()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public int Capacity { get; private set; }

    public TableStatus Status { get; private set; }

    public string QrToken { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static DiningTable Create(Guid tenantId, Guid branchId, string label, int capacity, string qrToken, DateTimeOffset now)
    {
        var table = new DiningTable
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            BranchId = branchId,
            Status = TableStatus.Available,
            QrToken = qrToken,
            CreatedAt = now,
        };
        table.Update(label, capacity);
        return table;
    }

    public void Update(string label, int capacity)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Trim().Length > 40)
        {
            throw new DomainException("table.invalid_label", "Table label is required (up to 40 characters).");
        }

        if (capacity is < 1 or > 50)
        {
            throw new DomainException("table.invalid_capacity", "Capacity must be between 1 and 50.");
        }

        Label = label.Trim();
        Capacity = capacity;
    }

    public void SetStatus(TableStatus status) => Status = status;

    /// <summary>Invalidates printed codes, e.g. after a QR sticker was copied or removed.</summary>
    public void RotateQr(string qrToken) => QrToken = qrToken;
}
