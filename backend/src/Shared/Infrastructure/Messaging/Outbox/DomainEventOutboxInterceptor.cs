using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RestaurantAI.SharedKernel.Domain;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Infrastructure.Messaging.Outbox;

/// <summary>
/// Converts domain events raised by tracked aggregates into outbox rows right before
/// SaveChanges, so business data and events are committed atomically.
/// </summary>
public sealed class DomainEventOutboxInterceptor(
    string module,
    ITenantContext tenantContext,
    ICorrelationContext correlationContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static readonly ConcurrentDictionary<Type, PropertyInfo?> TenantProperties = new();

    /// <summary>
    /// Platform administrators act without a tenant in their token; the event still belongs to the
    /// restaurant it describes, so take the tenant from the event's own TenantId when present.
    /// </summary>
    private static Guid? EventTenant(IDomainEvent domainEvent)
    {
        var property = TenantProperties.GetOrAdd(domainEvent.GetType(), type =>
            type.GetProperty("TenantId") is { } p && p.PropertyType == typeof(Guid) ? p : null);
        return property?.GetValue(domainEvent) is Guid id && id != Guid.Empty ? id : null;
    }

    private void Capture(DbContext? dbContext)
    {
        if (dbContext is null)
        {
            return;
        }

        foreach (var aggregate in dbContext.AggregatesWithEvents().ToList())
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                dbContext.Set<OutboxMessage>().Add(
                    OutboxMessage.From(domainEvent, module, tenantContext.TenantId ?? EventTenant(domainEvent), correlationContext.CorrelationId, tenantContext.UserId));
            }

            aggregate.ClearDomainEvents();
        }
    }
}
