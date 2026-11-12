namespace RestaurantAI.Gateway.Api.Middleware;

/// <summary>
/// Last-resort handler for failures inside the gateway itself. Upstream error responses are
/// proxied unchanged; only gateway faults are converted to problem details here.
/// </summary>
public sealed class GatewayExceptionMiddleware(RequestDelegate next, ILogger<GatewayExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted && ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Gateway failure for {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.Clear();
            await Results.Problem(
                title: "gateway_error",
                detail: "The gateway could not process the request.",
                statusCode: StatusCodes.Status502BadGateway,
                extensions: new Dictionary<string, object?> { ["correlationId"] = context.TraceIdentifier })
                .ExecuteAsync(context);
        }
    }
}
