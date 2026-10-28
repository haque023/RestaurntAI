using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.ServiceDefaults.Correlation;

/// <summary>
/// Reads the correlation id assigned by the gateway (or creates one for direct calls),
/// echoes it on the response and attaches it to the log scope and current trace.
/// </summary>
public sealed class CorrelationContextMiddleware(RequestDelegate next, ILogger<CorrelationContextMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[PlatformHeaders.CorrelationId].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
            context.Request.Headers[PlatformHeaders.CorrelationId] = correlationId;
        }

        context.TraceIdentifier = correlationId;
        context.Response.Headers[PlatformHeaders.CorrelationId] = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}
