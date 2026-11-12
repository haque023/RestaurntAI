using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using RestaurantAI.AI.Context;
using RestaurantAI.AI.Memory;
using RestaurantAI.SharedKernel.Tenancy;

namespace RestaurantAI.AI.Foundry.Agents;

public sealed record AgentTurnRequest(
    Guid TenantId,
    Guid? BranchId,
    Guid UserId,
    IReadOnlySet<string> Roles,
    Guid ConversationId,
    string Message,
    string UserAccessToken,
    IReadOnlyList<ContextItem>? LiveData = null,
    AgentProfile? Agent = null);

public enum AgentTurnOutcome
{
    Answered,
    Declined,
    Truncated,
}

public sealed record AgentTurnResponse(
    string Reply,
    Guid TurnId,
    AgentTurnOutcome Outcome,
    int ContextTokens,
    int EvictedItems,
    string Agent,
    long InputTokens,
    long OutputTokens,
    int ToolCalls,
    int LatencyMs);

/// <summary>
/// Runs one conversational turn: assemble context → call Claude with the MCP tools allowed for
/// the agent → persist the turn. Tool calls carry the end user's token, so the MCP Gateway and
/// business APIs authorize them exactly like a direct user request.
/// </summary>
public sealed class AgentConversationService(
    IChatClient chatClient,
    ClaudeRequestFactory requestFactory,
    ContextAssemblyService contextAssembly,
    IMemoryStore memory,
    FoundryMcpClientFactory mcpClientFactory,
    TimeProvider timeProvider,
    ILogger<AgentConversationService> logger)
{
    public const string DeclinedReply =
        "Sorry, I can't help with that request. A member of staff can assist you if you need anything else.";

    public async Task<AgentTurnResponse> RunTurnAsync(AgentTurnRequest request, CancellationToken cancellationToken)
    {
        var agent = request.Agent ?? AgentCatalog.ForRoles(request.Roles);
        var turnId = Guid.CreateVersion7();
        var started = timeProvider.GetTimestamp();

        var context = await contextAssembly.AssembleAsync(
            new ContextRequest(request.TenantId, request.BranchId, request.UserId, request.ConversationId, request.Message, agent.Instructions, request.LiveData ?? []),
            cancellationToken);

        await using var mcp = await mcpClientFactory.CreateAsync(request.UserAccessToken, request.ConversationId, turnId, cancellationToken);
        var tools = (await mcp.ListToolsAsync(cancellationToken: cancellationToken))
            .Where(tool => agent.AllowedTools.Contains(tool.Name))
            .Cast<AITool>()
            .ToList();

        var response = await chatClient.GetResponseAsync(context.Messages, requestFactory.CreateChatOptions(tools), cancellationToken);

        // stop_reason "refusal" (after any fallback) surfaces as ContentFilter; "max_tokens" as Length.
        var (reply, outcome) = response.FinishReason == ChatFinishReason.ContentFilter
            ? (DeclinedReply, AgentTurnOutcome.Declined)
            : response.FinishReason == ChatFinishReason.Length
                ? (response.Text + "\n\n(My answer was cut short. Ask me to continue.)", AgentTurnOutcome.Truncated)
                : (response.Text, AgentTurnOutcome.Answered);

        if (outcome != AgentTurnOutcome.Answered)
        {
            logger.LogWarning("Agent turn {TurnId} ended with {Outcome} ({FinishReason})", turnId, outcome, response.FinishReason?.Value);
        }

        var now = timeProvider.GetUtcNow();
        await memory.AppendTurnAsync(NewTurn(request, ConversationRole.User, request.Message, now), cancellationToken);
        await memory.AppendTurnAsync(NewTurn(request, ConversationRole.Assistant, reply, now), cancellationToken);

        var toolCalls = response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().Count();
        return new AgentTurnResponse(
            reply,
            turnId,
            outcome,
            context.TokensUsed,
            context.Evicted.Count,
            agent.Name,
            response.Usage?.InputTokenCount ?? 0,
            response.Usage?.OutputTokenCount ?? 0,
            toolCalls,
            (int)timeProvider.GetElapsedTime(started).TotalMilliseconds);
    }

    private static ConversationTurn NewTurn(AgentTurnRequest request, ConversationRole role, string content, DateTimeOffset at) => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = request.TenantId,
        ConversationId = request.ConversationId,
        UserId = request.UserId,
        Role = role,
        Content = content,
        TokenCount = TokenBudgetManager.EstimateTokens(content),
        CreatedAt = at,
    };
}

/// <summary>Creates per-turn MCP clients carrying the user's token and turn identifiers.</summary>
public sealed class FoundryMcpClientFactory(IOptions<ClaudeOptions> options)
{
    public Task<McpClient> CreateAsync(string userAccessToken, Guid conversationId, Guid turnId, CancellationToken cancellationToken)
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(options.Value.McpEndpoint),
            Name = "restaurant-mcp-gateway",
            AdditionalHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {userAccessToken}",
                [PlatformHeaders.ConversationId] = conversationId.ToString(),
                [PlatformHeaders.TurnId] = turnId.ToString(),
            },
        });

        return McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
    }
}
