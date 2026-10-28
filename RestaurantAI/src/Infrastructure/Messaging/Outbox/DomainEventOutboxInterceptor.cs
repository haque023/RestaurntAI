using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
                    OutboxMessage.From(domainEvent, module, tenantContext.TenantId, correlationContext.CorrelationId));
            }

            aggregate.ClearDomainEvents();
        }
    }
}
