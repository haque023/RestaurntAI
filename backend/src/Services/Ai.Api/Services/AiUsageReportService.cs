using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RestaurantAI.AI.Foundry;
using RestaurantAI.AI.Foundry.Agents;
using RestaurantAI.AI.Memory;
using RestaurantAI.SharedKernel.Tenancy;
using static RestaurantAI.AI.Api.Services.AiRequest;

namespace RestaurantAI.AI.Api.Services;

/// <summary>AI telemetry: a restaurant's own usage against its plan, and the platform-wide view for administrators.</summary>
public sealed class AiUsageReportService(IOptions<ClaudeOptions> claude, ITenantContext tenant, MemoryDbContext dbContext, AiUsageService usage, RestaurantAiContextClient restaurants, TimeProvider timeProvider) : IAiUsageReportService
{
    public IResult GetAgents() => TypedResults.Ok(new
    {
        model = claude.Value.Model,
        effort = claude.Value.Effort,
        maxOutputTokens = claude.Value.MaxOutputTokens,
        refusalFallback = claude.Value.RefusalFallback,
        agents = AgentCatalog.All.Select(a => new { a.Name, a.Description, tools = a.AllowedTools.Order(StringComparer.Ordinal), a.MaxToolCallsPerTurn }),
    });

    public async Task<IResult> GetCurrentAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var tenantId = tenant.TenantId!.Value;
        var start = usage.MonthStart;
        var rows = await dbContext.Usage.AsNoTracking().Where(u => u.TenantId == tenantId && u.CreatedAt >= start).ToListAsync(cancellationToken);
        var restaurant = await restaurants.GetAsync(tenantId, BearerToken(httpContext), cancellationToken);

        return TypedResults.Ok(new
        {
            periodStart = start,
            limit = restaurant?.AiTurnsPerMonth,
            aiEnabled = restaurant?.AiEnabled,
            usedTurns = rows.Count(r => r.Outcome != AiUsageService.FailedOutcome),
            summary = Summarize(rows),
        });
    }

    public async Task<IResult> GetPlatformUsageAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        var end = to ?? timeProvider.GetUtcNow();
        var start = from ?? usage.MonthStart;
        var rows = await dbContext.Usage.AsNoTracking().Where(u => u.CreatedAt >= start && u.CreatedAt < end).Take(200_000).ToListAsync(cancellationToken);

        var byTenant = rows
            .GroupBy(r => r.TenantId)
            .Select(g => new TenantUsage(g.Key, g.Count(r => r.Outcome != AiUsageService.FailedOutcome), g.Sum(r => r.InputTokens + r.OutputTokens), g.Count(r => r.Outcome == AiUsageService.FailedOutcome)))
            .OrderByDescending(t => t.Turns)
            .Take(100)
            .ToList();

        return TypedResults.Ok(new { from = start, to = end, summary = Summarize(rows), byTenant });
    }

    private static UsageSummary Summarize(IReadOnlyCollection<AiUsageRecord> rows) => new(
        rows.Count(r => r.Outcome != AiUsageService.FailedOutcome),
        rows.Sum(r => r.InputTokens),
        rows.Sum(r => r.OutputTokens),
        rows.Sum(r => r.ToolCalls),
        rows.Count(r => r.Outcome == AiUsageService.FailedOutcome),
        rows.Count(r => r.Outcome == nameof(AgentTurnOutcome.Declined)),
        rows.Count == 0 ? 0 : Math.Round(rows.Average(r => r.LatencyMs)),
        rows.GroupBy(r => r.Agent).ToDictionary(g => g.Key, g => g.Count()),
        rows.GroupBy(r => DateOnly.FromDateTime(r.CreatedAt.UtcDateTime))
            .OrderBy(g => g.Key)
            .Select(g => new DailyUsage(g.Key, g.Count(), g.Sum(r => r.InputTokens + r.OutputTokens)))
            .ToList());
}
