using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RestaurantAI.AI.Foundry;
using RestaurantAI.AI.Foundry.Agents;
using RestaurantAI.AI.Memory;

namespace RestaurantAI.AI.Api.Usage;

public sealed class AiUsageOptions
{
    public const string SectionName = "AI:Usage";

    /// <summary>Platform discovery has no restaurant plan, so it is capped per user per day.</summary>
    public int DiscoveryTurnsPerUserPerDay { get; set; } = 60;
}

/// <summary>Records AI telemetry per turn and enforces plan quotas (noisy-neighbour protection for AI).</summary>
public sealed class AiUsageService(MemoryDbContext dbContext, IOptions<ClaudeOptions> claude, IOptions<AiUsageOptions> options, TimeProvider timeProvider)
{
    public const string FailedOutcome = "Failed";

    public int DiscoveryDailyLimit => options.Value.DiscoveryTurnsPerUserPerDay;

    public DateTimeOffset MonthStart
    {
        get
        {
            var now = timeProvider.GetUtcNow();
            return new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        }
    }

    public Task<int> TurnsThisMonthAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var start = MonthStart;
        return dbContext.Usage.CountAsync(u => u.TenantId == tenantId && u.CreatedAt >= start && u.Outcome != FailedOutcome, cancellationToken);
    }

    public Task<int> DiscoveryTurnsTodayAsync(Guid userId, CancellationToken cancellationToken)
    {
        var since = timeProvider.GetUtcNow().AddDays(-1);
        return dbContext.Usage.CountAsync(u => u.UserId == userId && u.TenantId == null && u.CreatedAt >= since && u.Outcome != FailedOutcome, cancellationToken);
    }

    public async Task RecordAsync(Guid? tenantId, Guid userId, Guid conversationId, AgentTurnResponse turn, CancellationToken cancellationToken)
    {
        dbContext.Usage.Add(new AiUsageRecord
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            UserId = userId,
            ConversationId = conversationId,
            TurnId = turn.TurnId,
            Agent = turn.Agent,
            Model = claude.Value.Model,
            InputTokens = turn.InputTokens,
            OutputTokens = turn.OutputTokens,
            ToolCalls = turn.ToolCalls,
            Outcome = turn.Outcome.ToString(),
            LatencyMs = turn.LatencyMs,
            CreatedAt = timeProvider.GetUtcNow(),
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Failed model calls are recorded so outages show in AI telemetry; they never count against quotas.</summary>
    public async Task RecordFailureAsync(Guid? tenantId, Guid userId, Guid conversationId, string agent, int latencyMs, CancellationToken cancellationToken)
    {
        dbContext.Usage.Add(new AiUsageRecord
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            UserId = userId,
            ConversationId = conversationId,
            TurnId = Guid.CreateVersion7(),
            Agent = agent,
            Model = claude.Value.Model,
            Outcome = FailedOutcome,
            LatencyMs = latencyMs,
            CreatedAt = timeProvider.GetUtcNow(),
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
