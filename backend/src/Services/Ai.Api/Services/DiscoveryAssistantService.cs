using System.Diagnostics;
using System.Globalization;
using RestaurantAI.AI.Context;
using RestaurantAI.AI.Foundry.Agents;
using RestaurantAI.SharedKernel.Tenancy;
using static RestaurantAI.AI.Api.Services.AiRequest;

namespace RestaurantAI.AI.Api.Services;

/// <summary>
/// The platform discovery assistant: natural-language search for restaurants and dishes across
/// every listed restaurant, optionally near the customer. It has no restaurant (tenant) of its
/// own; history is kept per user in the distributed cache.
/// </summary>
public sealed class DiscoveryAssistantService(ITenantContext caller, AgentConversationService agents, AiUsageService usage, TimeProvider timeProvider, ILoggerFactory loggerFactory) : IDiscoveryAssistantService
{
    public async Task<IResult> TakeTurnAsync(DiscoveryTurnRequest request, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (ValidateMessage(request.Message) is { } invalid)
        {
            return invalid;
        }

        if (caller.UserId is not { } userId)
        {
            return Problem("auth.required", "Sign in to use the assistant.", StatusCodes.Status401Unauthorized);
        }

        if (request.Latitude.HasValue != request.Longitude.HasValue || request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
        {
            return Problem("discovery.invalid_location", "Provide a valid latitude and longitude together.", StatusCodes.Status400BadRequest);
        }

        if (await usage.DiscoveryTurnsTodayAsync(userId, cancellationToken) >= usage.DiscoveryDailyLimit)
        {
            return Problem("ai.quota_exceeded", "You have reached today's limit for the assistant. You can still search and filter restaurants yourself.", StatusCodes.Status429TooManyRequests);
        }

        var location = request.Latitude is { } lat && request.Longitude is { } lng
            ? string.Create(CultureInfo.InvariantCulture, $"The customer's current location is latitude {lat:F5}, longitude {lng:F5}. Search near it unless they name another place.")
            : "The customer's location is unknown. If distance matters, ask them to share their location (the app has a 'Use my location' button).";
        var live = new List<ContextItem>
        {
            new("discovery-context", ContextSource.LiveBusinessData, $"{location}\nCurrent time (UTC): {timeProvider.GetUtcNow():yyyy-MM-dd HH:mm}.", Pinned: true),
        };

        var conversationId = request.ConversationId ?? Guid.CreateVersion7();
        var started = Stopwatch.GetTimestamp();
        try
        {
            // Guid.Empty: a platform-level conversation, not owned by any restaurant.
            var response = await agents.RunTurnAsync(
                new AgentTurnRequest(Guid.Empty, null, userId, caller.Roles, conversationId, request.Message.Trim(), BearerToken(httpContext), live, AgentCatalog.Discovery),
                cancellationToken);

            await usage.RecordAsync(null, userId, conversationId, response, cancellationToken);
            return TypedResults.Ok(new DiscoveryTurnResponse(conversationId, response.TurnId, response.Reply, response.Outcome.ToString()));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggerFactory.CreateLogger(typeof(DiscoveryAssistantService)).LogError(ex, "Discovery assistant turn failed");
            await usage.RecordFailureAsync(null, userId, conversationId, AgentCatalog.Discovery.Name, (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds, cancellationToken);
            return Problem("ai.unavailable", "The assistant is unavailable at the moment. You can still search and filter restaurants yourself.", StatusCodes.Status503ServiceUnavailable);
        }
    }
}
