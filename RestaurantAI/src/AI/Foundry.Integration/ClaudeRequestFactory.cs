using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace RestaurantAI.AI.Foundry;

/// <summary>
/// Builds the Anthropic-specific request settings that Microsoft.Extensions.AI has no
/// abstraction for (effort, refusal fallback). The chat client merges messages, tools and the
/// model into these parameters and leaves the values set here untouched.
/// </summary>
public sealed class ClaudeRequestFactory(IOptions<ClaudeOptions> options)
{
    public const string ServerSideFallbackBeta = "server-side-fallback-2026-07-01";

    public ChatOptions CreateChatOptions(IList<AITool> tools)
    {
        var settings = options.Value;

        return new ChatOptions
        {
            Tools = tools,
            AllowMultipleToolCalls = true,
            RawRepresentationFactory = _ => new MessageCreateParams
            {
                Model = settings.Model,
                MaxTokens = settings.MaxOutputTokens,
                Messages = [],
                OutputConfig = new BetaOutputConfig { Effort = ParseEffort(settings.Effort) },
                Betas = settings.RefusalFallback ? [ServerSideFallbackBeta] : null,
                Fallbacks = settings.RefusalFallback ? new BetaFallbacksParam(new Default()) : null,
            },
        };
    }

    private static Effort ParseEffort(string effort) => effort.ToLowerInvariant() switch
    {
        "low" => Effort.Low,
        "high" => Effort.High,
        "xhigh" => Effort.Xhigh,
        "max" => Effort.Max,
        _ => Effort.Medium,
    };
}
