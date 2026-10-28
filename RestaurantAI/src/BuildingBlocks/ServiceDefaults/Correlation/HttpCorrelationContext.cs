using Microsoft.AspNetCore.Http;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.ServiceDefaults.Correlation;

internal sealed class HttpCorrelationContext(IHttpContextAccessor accessor) : ICorrelationContext
{
    public string? CorrelationId => accessor.HttpContext?.TraceIdentifier;
}
