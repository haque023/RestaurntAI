using System.Text.Json;
using RestaurantAI.SharedKernel.Domain;

namespace RestaurantAI.Infrastructure.Messaging.Outbox;

/// <summary>
/// A domain event persisted in the same transaction as the business change that produced it.
/// The <see cref="OutboxDispatcher{TContext}"/> later publishes it to RabbitMQ (at-least-once).
/// </summary>
public sealed class OutboxMessage
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Routing key / message type, e.g. <c>order.OrderCreated</c>.</summary>
    public string Type { get; private set; } = string.Empty;

    public string Payload { get; private set; } = "{}";

    public Guid? TenantId { get; private set; }

    public string? CorrelationId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage From(IDomainEvent domainEvent, string module, Guid? tenantId, string? correlationId) => new()
    {
        Id = domainEvent.EventId,
        Type = $"{module}.{domainEvent.GetType().Name}",
        Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions),
        TenantId = tenantId,
        CorrelationId = correlationId,
        OccurredAt = domainEvent.OccurredAt,
    };

    public void MarkProcessed(DateTimeOffset at)
    {
        ProcessedAt = at;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;
    }
}
