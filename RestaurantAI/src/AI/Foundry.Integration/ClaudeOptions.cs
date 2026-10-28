namespace RestaurantAI.AI.Foundry;

/// <summary>Configuration for the AI agents (section <c>AI:Claude</c>).</summary>
public sealed class ClaudeOptions
{
    public const string SectionName = "AI:Claude";

    /// <summary>
    /// Anthropic API key. Never commit it: set it with <c>dotnet user-secrets</c> locally and a
    /// secret store (Key Vault) in deployed environments. Falls back to <c>ANTHROPIC_API_KEY</c>.
    /// </summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-opus-5-5";

    /// <summary>Per-response output cap. Non-streaming calls stay well under SDK timeouts at this size.</summary>
    public int MaxOutputTokens { get; set; } = 16_000;

    /// <summary>
    /// Thinking depth / token spend: low, medium, high, xhigh or max. Claude Opus 5.5 defaults to
    /// medium; ordering through tools benefits from it, plain chit-chat would do with low.
    /// </summary>
    public string Effort { get; set; } = "medium";

    /// <summary>
    /// Server-side refusal fallback: if the model declines a request on policy grounds, Anthropic
    /// re-serves it on a substitute model chosen per refusal category, inside the same call.
    /// </summary>
    public bool RefusalFallback { get; set; } = true;

    /// <summary>MCP Gateway endpoint the agents use for tools.</summary>
    public string McpEndpoint { get; set; } = "http://localhost:5100/mcp";
}
