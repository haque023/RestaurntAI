using Microsoft.EntityFrameworkCore;
using RestaurantAI.ServiceDefaults.Errors;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Order.Api.Services;

/// <summary>Restaurant sales and operations KPIs for a period (default: the last 7 days).</summary>
public sealed class OrderStatsService(ITenantContext tenant, OrderDbContext dbContext, TimeProvider timeProvider) : IOrderStatsService
{
    private const int MaxOrders = 20_000;

    private static readonly OrderStatus[] Stopped = [OrderStatus.Cancelled, OrderStatus.Rejected];
    private static readonly OrderStatus[] Finished = [OrderStatus.Completed, OrderStatus.Cancelled, OrderStatus.Rejected];

    public async Task<IResult> GetStatsAsync(DateTimeOffset? from, DateTimeOffset? to, Guid? branchId, CancellationToken cancellationToken)
    {
        if (tenant.BranchId is { } own && branchId is { } requested && requested != own)
        {
            return OrderWorkflow.BranchForbidden.ToProblem();
        }

        var end = to ?? timeProvider.GetUtcNow();
        var start = from ?? end.AddDays(-7);
        if (start >= end || end - start > TimeSpan.FromDays(366))
        {
            return TypedResults.Problem(title: "stats.invalid_period", detail: "Choose a period of up to one year.", statusCode: StatusCodes.Status400BadRequest);
        }

        var query = dbContext.Orders.AsNoTracking().Where(o => o.PlacedAt >= start && o.PlacedAt < end);
        if ((branchId ?? tenant.BranchId) is { } branch)
        {
            query = query.Where(o => o.BranchId == branch);
        }

        var orders = await query.OrderByDescending(o => o.PlacedAt).Take(MaxOrders).ToListAsync(cancellationToken);
        var sold = orders.Where(o => !Stopped.Contains(o.Status)).ToList();
        var revenue = sold.Sum(o => o.Total);

        // Preparation time: accepted → ready, from the status history.
        var prepMinutes = orders
            .Select(o => (Accepted: o.History.FirstOrDefault(h => h.Status == OrderStatus.Accepted), Ready: o.History.FirstOrDefault(h => h.Status == OrderStatus.Ready)))
            .Where(p => p.Accepted is not null && p.Ready is not null)
            .Select(p => (p.Ready!.At - p.Accepted!.At).TotalMinutes)
            .ToList();

        var topItems = sold
            .SelectMany(o => o.Lines)
            .GroupBy(l => l.MenuItemId)
            .Select(g => new TopItem(g.Key, g.First().Name, g.Sum(l => l.Quantity), g.Sum(l => l.LineTotal)))
            .OrderByDescending(i => i.Quantity)
            .Take(10)
            .ToList();

        var daily = sold
            .GroupBy(o => DateOnly.FromDateTime(o.PlacedAt.UtcDateTime))
            .Select(g => new DailyPoint(g.Key, g.Count(), g.Sum(o => o.Total)))
            .OrderBy(d => d.Date)
            .ToList();

        return TypedResults.Ok(new OrderStats(
            start,
            end,
            orders.Select(o => o.Currency).FirstOrDefault(),
            orders.Count,
            orders.Count(o => o.Status == OrderStatus.Completed),
            orders.Count(o => o.Status == OrderStatus.Cancelled),
            orders.Count(o => o.Status == OrderStatus.Rejected),
            orders.Count(o => !Finished.Contains(o.Status)),
            revenue,
            sold.Count == 0 ? 0 : Math.Round(revenue / sold.Count, 2),
            prepMinutes.Count == 0 ? null : Math.Round(prepMinutes.Average(), 1),
            orders.GroupBy(o => o.Channel.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            orders.GroupBy(o => o.Status.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            topItems,
            daily));
    }
}
