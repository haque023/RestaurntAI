using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RestaurantAI.AI.Foundry.Agents;

namespace RestaurantAI.AI.Foundry;

public static class FoundryServiceCollectionExtensions
{
    /// <summary>
    /// Registers Claude (Anthropic API) behind the provider-neutral Microsoft.Extensions.AI
    /// <see cref="IChatClient"/>, with automatic tool invocation and OpenTelemetry instrumentation.
    /// The beta Messages API is used so refusal fallbacks can be enabled per request.
    /// </summary>
    public static IServiceCollection AddClaude(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ClaudeOptions>(configuration.GetSection(ClaudeOptions.SectionName));

        services.AddSingleton<IAnthropicClient>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<ClaudeOptions>>().Value;
            var apiKey = !string.IsNullOrWhiteSpace(options.ApiKey) ? options.ApiKey : Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    $"No Anthropic API key configured. Set '{ClaudeOptions.SectionName}:ApiKey' with dotnet user-secrets or the ANTHROPIC_API_KEY environment variable.");
            }

            return new AnthropicClient { ApiKey = apiKey };
        });

        services.AddChatClient(provider =>
            {
                var options = provider.GetRequiredService<IOptions<ClaudeOptions>>().Value;
                return provider.GetRequiredService<IAnthropicClient>().Beta.AsIChatClient(options.Model, options.MaxOutputTokens);
            })
            .UseFunctionInvocation(configure: invocation => invocation.MaximumIterationsPerRequest = 8)
            .UseOpenTelemetry(sourceName: "RestaurantAI.AI");

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ClaudeRequestFactory>();
        services.AddSingleton<FoundryMcpClientFactory>();
        services.AddScoped<AgentConversationService>();

        return services;
    }
}
