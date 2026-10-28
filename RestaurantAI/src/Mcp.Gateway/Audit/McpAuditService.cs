using System.Diagnostics;
using System.Text.Json;
using RestaurantAI.Infrastructure.Messaging.RabbitMQ;
using RestaurantAI.Mcp.Gateway.Context;

namespace RestaurantAI.Mcp.Gateway.Audit;

/// <summary>
/// Audits every AI tool execution (a critical operation per the audit requirements):
/// user, tenant, branch, tool, outcome, duration and correlation id. Written as structured logs
/// and published to the event bus (<c>mcp.ToolExecuted</c>), where the platform audit log stores it.
/// </summary>
public sealed partial class McpAuditService(ILogger<McpAuditService> logger, IHttpContextAccessor accessor, IEventPublisher publisher, TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private string? CorrelationId => accessor.HttpContext?.Request.Headers[RestaurantAI.SharedKernel.Tenancy.PlatformHeaders.CorrelationId].FirstOrDefault()
        ?? accessor.HttpContext?.TraceIdentifier;

    public async Task<string> TrackAsync(string toolName, ToolCallerContext caller, Func<Task<string>> execute)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await execute();
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            LogExecuted(toolName, caller.User.UserId, caller.Restaurant.TenantId, caller.Branch.BranchId, "succeeded", elapsed, CorrelationId);
            await PublishAsync(toolName, caller.User.UserId, caller.Restaurant.TenantId, caller.Branch.BranchId, caller.ConversationId, "succeeded", elapsed, null);
            return result;
        }
        catch (Exception ex)
        {
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            LogExecuted(toolName, caller.User.UserId, caller.Restaurant.TenantId, caller.Branch.BranchId, $"failed:{ex.GetType().Name}", elapsed, CorrelationId);
            await PublishAsync(toolName, caller.User.UserId, caller.Restaurant.TenantId, caller.Branch.BranchId, caller.ConversationId, "failed", elapsed, ex.Message);
            throw;
        }
    }

    public void Denied(string toolName, ToolCallerContext? caller, string reason)
    {
        LogDenied(toolName, caller?.User.UserId, caller?.Restaurant.TenantId, reason, CorrelationId);
        _ = PublishAsync(toolName, caller?.User.UserId, caller?.Restaurant.TenantId, caller?.Branch.BranchId, caller?.ConversationId, "denied", 0, reason);
    }

    /// <summary>Best effort: auditing must never break the tool call, and logs remain the fallback record.</summary>
    private async Task PublishAsync(string tool, Guid? userId, Guid? tenantId, Guid? branchId, string? conversationId, string outcome, double durationMs, string? detail)
    {
        try
        {
            var tenant = tenantId is { } t && t != Guid.Empty ? t : (Guid?)null;
            var payload = JsonSerializer.Serialize(new
            {
                tool,
                userId,
                tenantId = tenant,
                branchId,
                conversationId,
                outcome,
                durationMs = Math.Round(durationMs),
                detail = detail is { Length: > 300 } ? detail[..300] : detail,
            }, Json);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await publisher.PublishAsync(
                new IntegrationMessage(Guid.CreateVersion7(), "mcp.ToolExecuted", payload, tenant, CorrelationId, timeProvider.GetUtcNow(), userId),
                timeout.Token);
        }
        catch (Exception ex)
        {
            LogPublishFailed(tool, ex.Message);
        }
    }

    [LoggerMessage(EventId = 7003, Level = LogLevel.Warning, Message = "Could not publish audit event for {Tool}: {Error}")]
    private partial void LogPublishFailed(string tool, string error);

    [LoggerMessage(EventId = 7001, Level = LogLevel.Information,
        Message = "AUDIT mcp.tool {Tool} user={UserId} tenant={TenantId} branch={BranchId} outcome={Outcome} durationMs={DurationMs} correlationId={CorrelationId}")]
    private partial void LogExecuted(string tool, Guid userId, Guid tenantId, Guid? branchId, string outcome, double durationMs, string? correlationId);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Warning,
        Message = "AUDIT mcp.tool.denied {Tool} user={UserId} tenant={TenantId} reason={Reason} correlationId={CorrelationId}")]
    private partial void LogDenied(string tool, Guid? userId, Guid? tenantId, string reason, string? correlationId);
}
