using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Admin.Api.Services;

/// <summary>
/// Platform administration: the platform overview and the cross-tenant audit log. Restaurant
/// lifecycle, users and AI governance live with the services that own that data (also under
/// /api/admin). Every endpoint runs under the separate privileged platform-administrator policy.
/// </summary>
public sealed class AdminService(IHostEnvironment environment, TimeProvider timeProvider, PlatformDbContext dbContext) : IAdminService
{
    private sealed record OrderCreatedPayload(decimal Total, string Currency);

    public async Task<IResult> OverviewAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var dayAgo = now.AddDays(-1);

        var last24h = await dbContext.AuditEvents.AsNoTracking()
            .Where(e => e.OccurredAt >= dayAgo)
            .GroupBy(e => e.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var monthTypes = new[] { "order.OrderCreated", "restaurant.RestaurantRegistered", "identity.UserRegistered", "mcp.ToolExecuted", "reservation.ReservationCreated" };
        var month = await dbContext.AuditEvents.AsNoTracking()
            .Where(e => e.OccurredAt >= monthStart && monthTypes.Contains(e.Type))
            .GroupBy(e => e.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Type, g => g.Count, cancellationToken);

        // Gross order value this month per currency, from the order events.
        var orderPayloads = await dbContext.AuditEvents.AsNoTracking()
            .Where(e => e.OccurredAt >= monthStart && e.Type == "order.OrderCreated")
            .Select(e => e.Payload)
            .Take(100_000)
            .ToListAsync(cancellationToken);
        var gross = orderPayloads
            .Select(p => JsonSerializer.Deserialize<OrderCreatedPayload>(p, JsonSerializerOptions.Web))
            .Where(p => p is not null)
            .GroupBy(p => p!.Currency)
            .ToDictionary(g => g.Key, g => g.Sum(p => p!.Total));

        return TypedResults.Ok(new
        {
            environment = environment.EnvironmentName,
            serverTime = now,
            month = new
            {
                from = monthStart,
                orders = month.GetValueOrDefault("order.OrderCreated"),
                grossOrderValue = gross,
                reservations = month.GetValueOrDefault("reservation.ReservationCreated"),
                newRestaurants = month.GetValueOrDefault("restaurant.RestaurantRegistered"),
                newUsers = month.GetValueOrDefault("identity.UserRegistered"),
                aiToolCalls = month.GetValueOrDefault("mcp.ToolExecuted"),
            },
            last24h = last24h.OrderByDescending(e => e.Count).ToDictionary(e => e.Type, e => e.Count),
        });
    }

    public async Task<IResult> AuditAsync(string? type, string? module, Guid? tenantId, Guid? actorId, DateTimeOffset? from, DateTimeOffset? to, int? take, CancellationToken cancellationToken)
    {
        var query = dbContext.AuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(e => e.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(module))
        {
            query = query.Where(e => e.Module == module);
        }

        if (tenantId is { } tenant)
        {
            query = query.Where(e => e.TenantId == tenant);
        }

        if (actorId is { } actor)
        {
            query = query.Where(e => e.ActorId == actor);
        }

        if (from is { } start)
        {
            query = query.Where(e => e.OccurredAt >= start);
        }

        if (to is { } end)
        {
            query = query.Where(e => e.OccurredAt < end);
        }

        var rows = await query.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).Take(Math.Clamp(take ?? 100, 1, 500)).ToListAsync(cancellationToken);
        return TypedResults.Ok(rows.Select(e => new AuditEventDto(
            e.Id, e.Type, e.Module, e.TenantId, e.ActorId, e.CorrelationId, JsonSerializer.Deserialize<JsonElement>(e.Payload), e.OccurredAt)).ToList());
    }
}
