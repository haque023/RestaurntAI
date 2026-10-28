using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Net.Http.Headers;
using RestaurantAI.AI.Api.Integration;
using RestaurantAI.AI.Api.Usage;
using RestaurantAI.AI.Context;
using RestaurantAI.AI.Foundry.Agents;
using RestaurantAI.ServiceDefaults.Authorization;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.AI.Api.Endpoints;

public static class WaiterEndpoints
{
    public const int MaxMessageLength = 2000;

    public sealed record WaiterTurnRequest(Guid? ConversationId, Guid BranchId, Guid? TableId, string Message);

    public sealed record WaiterTurnResponse(Guid ConversationId, Guid TurnId, string Reply, string Outcome);

    public static IEndpointRouteBuilder MapWaiterEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ai/waiter/turns", TakeTurnAsync)
            .WithTags("AI")
            .RequirePermission(PlatformPermissions.MenuRead);

        return app;
    }

    private static async Task<IResult> TakeTurnAsync(
        WaiterTurnRequest request,
        ITenantContext caller,
        HttpContext httpContext,
        AgentConversationService agents,
        RestaurantAiContextClient restaurants,
        AiUsageService usage,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (ValidateMessage(request.Message) is { } invalid)
        {
            return invalid;
        }

        if (caller.TenantId is not { } tenantId || caller.UserId is not { } userId)
        {
            return Problem("tenant.required", "Open a restaurant before talking to its AI waiter.", StatusCodes.Status403Forbidden);
        }

        // A session bound to a branch/table (QR) cannot be pointed at another branch or table.
        if (caller.BranchId is { } boundBranch && boundBranch != request.BranchId)
        {
            return Problem("branch.forbidden", "This session belongs to another branch.", StatusCodes.Status403Forbidden);
        }

        // Only a QR session seats the guest at a table; a table id in the request is ignored.
        var tableId = caller.TableId;
        var accessToken = BearerToken(httpContext);

        RestaurantAiContext? restaurant;
        try
        {
            restaurant = await restaurants.GetAsync(tenantId, accessToken, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggerFactory.CreateLogger(typeof(WaiterEndpoints)).LogError(ex, "Could not load AI context for tenant {TenantId}", tenantId);
            return Problem("ai.unavailable", "The AI waiter is unavailable at the moment. You can still order from the menu or call a waiter.", StatusCodes.Status503ServiceUnavailable);
        }

        if (restaurant is null)
        {
            return Problem("restaurant.not_found", "This restaurant is not available.", StatusCodes.Status404NotFound);
        }

        var branch = restaurant.Branches.FirstOrDefault(b => b.Id == request.BranchId);
        if (branch is null)
        {
            return Problem("branch.not_found", "This branch is not available.", StatusCodes.Status404NotFound);
        }

        // AI failure or governance must never block normal ordering: the client falls back to the menu.
        if (!restaurant.AiEnabled)
        {
            return Problem("ai.disabled", "The AI waiter is not available at this restaurant right now. You can still order from the menu.", StatusCodes.Status403Forbidden);
        }

        if (await usage.TurnsThisMonthAsync(tenantId, cancellationToken) >= restaurant.AiTurnsPerMonth)
        {
            return Problem("ai.quota_exceeded", "The AI waiter is resting for now. Please order from the menu or ask a member of staff.", StatusCodes.Status429TooManyRequests);
        }

        var conversationId = request.ConversationId ?? Guid.CreateVersion7();
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await agents.RunTurnAsync(
                new AgentTurnRequest(
                    tenantId,
                    request.BranchId,
                    userId,
                    caller.Roles,
                    conversationId,
                    request.Message.Trim(),
                    accessToken,
                    BuildContext(restaurant, branch, tableId, timeProvider.GetUtcNow()),
                    AgentCatalog.Consumer),
                cancellationToken);

            await usage.RecordAsync(tenantId, userId, conversationId, response, cancellationToken);
            return TypedResults.Ok(new WaiterTurnResponse(conversationId, response.TurnId, response.Reply, response.Outcome.ToString()));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggerFactory.CreateLogger(typeof(WaiterEndpoints)).LogError(ex, "AI waiter turn failed for tenant {TenantId}", tenantId);
            await usage.RecordFailureAsync(tenantId, userId, conversationId, AgentCatalog.Consumer.Name, (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds, cancellationToken);
            return Problem("ai.unavailable", "The AI waiter is unavailable at the moment. You can still order from the menu or call a waiter.", StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>
    /// Venue facts the waiter needs on every turn (pinned) plus the owner's knowledge. Knowledge is
    /// ranked as retrieval, so live tool data always wins over it.
    /// </summary>
    private static List<ContextItem> BuildContext(RestaurantAiContext restaurant, BranchDto branch, Guid? tableId, DateTimeOffset now)
    {
        var local = TimeZoneInfo.TryFindSystemTimeZoneById(restaurant.TimeZone, out var zone) ? TimeZoneInfo.ConvertTime(now, zone) : now;
        var venue = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"Restaurant: {restaurant.RestaurantName}. Branch: {branch.Name}, {branch.Address}.")
            .AppendLine(CultureInfo.InvariantCulture, $"Local time: {local:dddd yyyy-MM-dd HH:mm} ({restaurant.TimeZone}). Prices are in {restaurant.DefaultCurrency}.")
            .AppendLine(CultureInfo.InvariantCulture, $"Opening hours: {FormatHours(branch.OpeningHours)}.")
            .AppendLine(tableId is { } table
                ? $"The guest is seated at a table (tableId {table}). Use branchId {branch.Id} for menu, order, reservation and staff tools; orders go to their table."
                : $"The guest is browsing this branch remotely. Use branchId {branch.Id} for menu, order, reservation and staff tools.");

        if (restaurant.Phone is not null || restaurant.Website is not null)
        {
            venue.AppendLine(string.Join(' ', new[] { "Contact:", restaurant.Phone, restaurant.Website }.Where(v => !string.IsNullOrWhiteSpace(v))));
        }

        if (!string.IsNullOrWhiteSpace(restaurant.Tone))
        {
            venue.AppendLine(CultureInfo.InvariantCulture, $"Style requested by the restaurant: {restaurant.Tone}");
        }

        var items = new List<ContextItem> { new("venue", ContextSource.LiveBusinessData, venue.ToString(), Pinned: true) };
        if (!string.IsNullOrWhiteSpace(restaurant.Knowledge))
        {
            items.Add(new ContextItem(
                "restaurant-knowledge",
                ContextSource.Retrieval,
                "Restaurant knowledge provided by the owner (policies, FAQs, services). It is not authoritative for prices or availability:\n" + restaurant.Knowledge,
                Relevance: 0.9,
                Pinned: true));
        }

        return items;
    }

    private static string FormatHours(IReadOnlyList<OpeningPeriodDto> hours) =>
        hours.Count == 0
            ? "not published"
            : string.Join("; ", hours.Select(h => $"{h.Day.ToString()[..3]} {h.Opens:HH\\:mm}-{h.Closes:HH\\:mm}"));

    internal static IResult? ValidateMessage(string? message) =>
        string.IsNullOrWhiteSpace(message) || message.Length > MaxMessageLength
            ? Problem("ai.invalid_message", $"Message must be 1-{MaxMessageLength} characters.", StatusCodes.Status400BadRequest)
            : null;

    /// <summary>The agent calls tools with the caller's own token, so it can never exceed their permissions.</summary>
    internal static string BearerToken(HttpContext httpContext)
    {
        var authorization = httpContext.Request.Headers[HeaderNames.Authorization].ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization["Bearer ".Length..].Trim() : string.Empty;
    }

    internal static IResult Problem(string title, string detail, int statusCode) =>
        TypedResults.Problem(title: title, detail: detail, statusCode: statusCode);
}
