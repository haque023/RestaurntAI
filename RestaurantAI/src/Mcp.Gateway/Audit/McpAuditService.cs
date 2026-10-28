using System.Diagnostics;
using RestaurantAI.Mcp.Gateway.Context;

namespace RestaurantAI.Mcp.Gateway.Audit;

/// <summary>
/// Audits every AI tool execution (a critical operation per the audit requirements):
/// user, tenant, branch, tool, outcome, duration and correlation id.
/// Emitted as structured logs today; can be forwarded to the Audit stream via RabbitMQ.
/// </summary>
public sealed partial class McpAuditService(ILogger<McpAuditService> logger, IHttpContextAccessor accessor)
{
    private string? CorrelationId => accessor.HttpContext?.TraceIdentifier;

    public async Task<string> TrackAsync(string toolName, ToolCallerContext caller, Func<Task<string>> execute)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await execute();
            LogExecuted(toolName, caller.User.UserId, caller.Restaurant.TenantId, caller.Branch.BranchId, "succeeded", Stopwatch.GetElapsedTime(started).TotalMilliseconds, CorrelationId);
            return result;
        }
        catch (Exception ex)
        {
            LogExecuted(toolName, caller.User.UserId, caller.Restaurant.TenantId, caller.Branch.BranchId, $"failed:{ex.GetType().Name}", Stopwatch.GetElapsedTime(started).TotalMilliseconds, CorrelationId);
            throw;
        }
    }

    public void Denied(string toolName, ToolCallerContext? caller, string reason) =>
        LogDenied(toolName, caller?.User.UserId, caller?.Restaurant.TenantId, reason, CorrelationId);

    [LoggerMessage(EventId = 7001, Level = LogLevel.Information,
        Message = "AUDIT mcp.tool {Tool} user={UserId} tenant={TenantId} branch={BranchId} outcome={Outcome} durationMs={DurationMs} correlationId={CorrelationId}")]
    private partial void LogExecuted(string tool, Guid userId, Guid tenantId, Guid? branchId, string outcome, double durationMs, string? correlationId);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Warning,
        Message = "AUDIT mcp.tool.denied {Tool} user={UserId} tenant={TenantId} reason={Reason} correlationId={CorrelationId}")]
    private partial void LogDenied(string tool, Guid? userId, Guid? tenantId, string reason, string? correlationId);
}
