using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.ServiceDefaults.Http;

/// <summary>
/// Forwards the caller's bearer token and correlation id on service-to-service calls, so the
/// downstream service applies the same User + Tenant + Branch authorization.
/// </summary>
public sealed class ForwardCallerContextHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = accessor.HttpContext;
        if (context is not null)
        {
            var authorization = context.Request.Headers[HeaderNames.Authorization].ToString();
            if (!string.IsNullOrEmpty(authorization) && request.Headers.Authorization is null)
            {
                request.Headers.TryAddWithoutValidation(HeaderNames.Authorization, authorization);
            }

            if (!request.Headers.Contains(PlatformHeaders.CorrelationId))
            {
                request.Headers.TryAddWithoutValidation(PlatformHeaders.CorrelationId, context.TraceIdentifier);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
