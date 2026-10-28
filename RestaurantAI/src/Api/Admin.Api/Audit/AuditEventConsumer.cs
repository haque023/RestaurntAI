using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RestaurantAI.Admin.Api.Persistence;
using RestaurantAI.Infrastructure.Messaging.RabbitMQ;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Admin.Api.Audit;

/// <summary>
/// Subscribes a durable queue to every event on the platform exchange and stores it in the audit
/// log. Reconnects with backoff, so a broker outage delays auditing but never loses events
/// (they wait in the queue, or in each module's outbox while the broker is down).
/// </summary>
public sealed partial class AuditEventConsumer(
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    ILogger<AuditEventConsumer> logger) : BackgroundService
{
    public const string QueueName = "platform.audit";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(2);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
                delay = TimeSpan.FromSeconds(2);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogConnectionFailed(ex.Message, delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 60));
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var factory = new ConnectionFactory { Uri = new Uri(settings.ConnectionString), ClientProvidedName = settings.ClientName };
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(settings.Exchange, ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await channel.QueueBindAsync(QueueName, settings.Exchange, "#", cancellationToken: stoppingToken);
        await channel.BasicQosAsync(0, 50, false, stoppingToken);

        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.ConnectionShutdownAsync += (_, _) =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                await StoreAsync(delivery, stoppingToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogStoreFailed(delivery.BasicProperties.Type, ex.Message);
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: !delivery.Redelivered, stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);
        LogConsuming(QueueName);
        await closed.Task.WaitAsync(stoppingToken);
        throw new InvalidOperationException("The broker connection closed.");
    }

    private async Task StoreAsync(BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        var properties = delivery.BasicProperties;
        var id = Guid.TryParse(properties.MessageId, out var messageId) ? messageId : Guid.CreateVersion7();
        var type = properties.Type ?? delivery.RoutingKey;

        await using var scope = scopes.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        if (await dbContext.AuditEvents.AnyAsync(e => e.Id == id, cancellationToken))
        {
            return;
        }

        dbContext.AuditEvents.Add(new AuditEvent
        {
            Id = id,
            Type = type,
            Module = type.Split('.')[0],
            TenantId = Header(properties, "tenant_id") is { } tenant && Guid.TryParse(tenant, out var tenantId) ? tenantId : null,
            ActorId = Header(properties, "actor_id") is { } actor && Guid.TryParse(actor, out var actorId) ? actorId : null,
            CorrelationId = properties.CorrelationId ?? Header(properties, PlatformHeaders.CorrelationId),
            Payload = Encoding.UTF8.GetString(delivery.Body.Span),
            OccurredAt = properties.Timestamp.UnixTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(properties.Timestamp.UnixTime) : timeProvider.GetUtcNow(),
            ReceivedAt = timeProvider.GetUtcNow(),
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? Header(IReadOnlyBasicProperties properties, string name) =>
        properties.Headers?.TryGetValue(name, out var value) == true
            ? value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string text => text,
                _ => null,
            }
            : null;

    [LoggerMessage(EventId = 8001, Level = LogLevel.Information, Message = "Audit consumer listening on queue {Queue}")]
    private partial void LogConsuming(string queue);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Warning, Message = "Audit consumer disconnected ({Error}); retrying in {Seconds}s")]
    private partial void LogConnectionFailed(string error, double seconds);

    [LoggerMessage(EventId = 8003, Level = LogLevel.Error, Message = "Could not store audit event {Type}: {Error}")]
    private partial void LogStoreFailed(string? type, string error);
}
