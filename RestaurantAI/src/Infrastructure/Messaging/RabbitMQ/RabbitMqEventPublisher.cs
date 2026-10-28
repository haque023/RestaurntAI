using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Infrastructure.Messaging.RabbitMQ;

/// <summary>
/// Publishes integration messages to a durable topic exchange with publisher confirms.
/// Notifications, Loyalty, Analytics, Memory and Audit workers bind their own queues to it.
/// </summary>
public sealed class RabbitMqEventPublisher(IOptions<RabbitMqOptions> options) : IEventPublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync(IntegrationMessage message, CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync(cancellationToken);

        var properties = new BasicProperties
        {
            MessageId = message.MessageId.ToString(),
            Type = message.Type,
            CorrelationId = message.CorrelationId,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(message.OccurredAt.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>
            {
                ["tenant_id"] = message.TenantId?.ToString(),
                [PlatformHeaders.CorrelationId] = message.CorrelationId,
                ["actor_id"] = message.ActorId?.ToString(),
            },
        };

        await channel.BasicPublishAsync(
            exchange: options.Value.Exchange,
            routingKey: message.Type,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(message.Payload),
            cancellationToken: cancellationToken);
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            var settings = options.Value;
            var factory = new ConnectionFactory
            {
                Uri = new Uri(settings.ConnectionString),
                ClientProvidedName = settings.ClientName,
                AutomaticRecoveryEnabled = true,
            };

            _connection ??= await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                cancellationToken);
            await _channel.ExchangeDeclareAsync(settings.Exchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);

            return _channel;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
