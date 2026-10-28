using System.Diagnostics;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.Gateway.Api.Middleware;

/// <summary>
/// Origin of the correlation id for every request entering the platform. A client-supplied id
/// is accepted only if it is well-formed; otherwise a new one is generated. The id is echoed on
/// the response and forwarded upstream by the proxy transform.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[PlatformHeaders.CorrelationId].FirstOrDefault();
        var correlationId = IsValid(incoming) ? incoming! : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[PlatformHeaders.CorrelationId] = correlationId;
            return Task.CompletedTask;
        });
        Activity.Current?.SetTag("correlation.id", correlationId);

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    public static bool IsValid(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= MaxLength
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
