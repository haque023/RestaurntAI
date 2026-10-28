using Microsoft.Net.Http.Headers;
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
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > MaxMessageLength)
        {
            return TypedResults.Problem(title: "ai.invalid_message", detail: $"Message must be 1-{MaxMessageLength} characters.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (caller.TenantId is not { } tenantId || caller.UserId is not { } userId)
        {
            return TypedResults.Problem(title: "tenant.required", detail: "Open a restaurant before talking to its AI waiter.", statusCode: StatusCodes.Status403Forbidden);
        }

        // The agent calls tools with the guest's own token, so it can never exceed their permissions.
        var authorization = httpContext.Request.Headers[HeaderNames.Authorization].ToString();
        var accessToken = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization["Bearer ".Length..].Trim() : string.Empty;

        var conversationId = request.ConversationId ?? Guid.CreateVersion7();
        var venue = request.TableId is { } table
            ? $"The guest is seated at table {table} in branch {request.BranchId}."
            : $"The guest is browsing branch {request.BranchId}.";

        var response = await agents.RunTurnAsync(
            new AgentTurnRequest(
                tenantId,
                request.BranchId,
                userId,
                caller.Roles,
                conversationId,
                request.Message.Trim(),
                accessToken,
                [
                    new ContextItem(
                        "venue",
                        ContextSource.LiveBusinessData,
                        $"{venue} Use branchId {request.BranchId} for menu, order and reservation tools." +
                        (request.TableId is { } t ? $" Use tableId {t} for orders at the table." : string.Empty),
                        Pinned: true),
                ]),
            cancellationToken);

        return TypedResults.Ok(new WaiterTurnResponse(conversationId, response.TurnId, response.Reply, response.Outcome.ToString()));
    }
}
