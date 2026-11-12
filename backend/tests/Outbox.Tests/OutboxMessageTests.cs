using System.Text.Json;
using RestaurantAI.Infrastructure.Messaging.Outbox;
using RestaurantAI.Order.Api.IServices;
using RestaurantAI.Order.Api.Models;
using RestaurantAI.Order.Api.Services;

namespace RestaurantAI.Outbox.Tests;

public sealed class OutboxMessageTests
{
    [Fact]
    public void From_uses_module_qualified_type_and_event_id()
    {
        var domainEvent = new OrderCreated(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 25m, "USD");

        var message = OutboxMessage.From(domainEvent, "order", domainEvent.TenantId, "corr-1");

        Assert.Equal("order.OrderCreated", message.Type);
        Assert.Equal(domainEvent.EventId, message.Id);
        Assert.Equal("corr-1", message.CorrelationId);
        Assert.Null(message.ProcessedAt);
    }

    [Fact]
    public void Payload_is_camel_case_json_of_the_concrete_event()
    {
        var domainEvent = new OrderCreated(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 25m, "USD");

        var payload = JsonDocument.Parse(OutboxMessage.From(domainEvent, "order", null, null).Payload).RootElement;

        Assert.Equal(domainEvent.OrderId, payload.GetProperty("orderId").GetGuid());
        Assert.Equal(25m, payload.GetProperty("total").GetDecimal());
    }

    [Fact]
    public void MarkFailed_counts_attempts_and_truncates_errors()
    {
        var message = OutboxMessage.From(new OrderCancelled(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "x"), "order", null, null);

        message.MarkFailed(new string('e', 5000));
        message.MarkFailed("broker down");

        Assert.Equal(2, message.Attempts);
        Assert.Equal("broker down", message.LastError);
    }

    [Fact]
    public void MarkProcessed_clears_the_last_error()
    {
        var message = OutboxMessage.From(new OrderCancelled(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "x"), "order", null, null);
        message.MarkFailed("broker down");

        message.MarkProcessed(DateTimeOffset.UtcNow);

        Assert.NotNull(message.ProcessedAt);
        Assert.Null(message.LastError);
    }
}
