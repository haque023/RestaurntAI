namespace RestaurantAI.SharedKernel.Domain;

/// <summary>
/// A fact that happened inside an aggregate. Domain events are persisted to the
/// outbox in the same transaction as the aggregate and dispatched to RabbitMQ afterwards.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
