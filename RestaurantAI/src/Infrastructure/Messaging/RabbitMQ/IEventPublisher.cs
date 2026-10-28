namespace RestaurantAI.Infrastructure.Messaging.RabbitMQ;

/// <summary>An event as it travels on the bus.</summary>
public sealed record IntegrationMessage(
    Guid MessageId,
    string Type,
    string Payload,
    Guid? TenantId,
    string? CorrelationId,
    DateTimeOffset OccurredAt);

public interface IEventPublisher
{
    Task PublishAsync(IntegrationMessage message, CancellationToken cancellationToken);
}
