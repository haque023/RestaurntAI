using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RestaurantAI.AI.Api.Integration;
using RestaurantAI.AI.Api.Usage;
using RestaurantAI.AI.Foundry;
using RestaurantAI.AI.Foundry.Agents;
using RestaurantAI.AI.Memory;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;
using static RestaurantAI.AI.Api.Endpoints.WaiterEndpoints;

namespace RestaurantAI.AI.Api.Endpoints;

/// <summary>AI telemetry: a restaurant's own usage against its plan, and the platform-wide view for administrators.</summary>
public static class AiUsageEndpoints
{
    public sealed record UsageSummary(
        int Turns,
        long InputTokens,
        long OutputTokens,
        int ToolCalls,
        int Failed,
        int Declined,
        double AverageLatencyMs,
        IReadOnlyDictionary<string, int> ByAgent,
        IReadOnlyList<DailyUsage> Daily);

    public sealed record DailyUsage(DateOnly Date, int Turns, long Tokens);

    public sealed record TenantUsage(Guid? TenantId, int Turns, long Tokens, int Failed);

    public static IEndpointRouteBuilder MapAiUsageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/ai/usage/current", GetCurrentAsync).WithTags("AI").RequirePermission(PlatformPermissions.AnalyticsRead);

        var admin = app.MapGroup("/api/admin/ai")
            .WithTags("Admin")
            .RequireAuthorization(policy => policy.RequireRole(PlatformRoles.PlatformAdministrator));
        admin.MapGet("/usage", GetPlatformUsageAsync);
        admin.MapGet("/agents", (IOptions<ClaudeOptions> claude) => TypedResults.Ok(new
        {
            model = claude.Value.Model,
            effort = claude.Value.Effort,
            maxOutputTokens = claude.Value.MaxOutputTokens,
            refusalFallback = claude.Value.RefusalFallback,
            agents = AgentCatalog.All.Select(a => new { a.Name, a.Description, tools = a.AllowedTools.Order(StringComparer.Ordinal), a.MaxToolCallsPerTurn }),
        }));

        return app;
    }

    private static async Task<IResult> GetCurrentAsync(
        ITenantContext tenant,
        HttpContext httpContext,
        MemoryDbContext dbContext,
        AiUsageService usage,
        RestaurantAiContextClient restaurants,
        CancellationToken cancellationToken)
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

    private static async Task<IResult> GetPlatformUsageAsync(DateTimeOffset? from, DateTimeOffset? to, MemoryDbContext dbContext, AiUsageService usage, TimeProvider timeProvider, CancellationToken cancellationToken)
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
