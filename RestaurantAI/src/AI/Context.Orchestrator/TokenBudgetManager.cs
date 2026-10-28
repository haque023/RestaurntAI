namespace RestaurantAI.AI.Context;

public sealed class TokenBudgetOptions
{
    /// <summary>Model context window in tokens.</summary>
    public int ContextWindow { get; init; } = 128_000;

    /// <summary>Tokens reserved for the model's answer.</summary>
    public int ReservedForOutput { get; init; } = 4_000;

    /// <summary>Tokens reserved for tool schemas sent alongside the prompt.</summary>
    public int ReservedForTools { get; init; } = 6_000;

    /// <summary>Hard cap on input tokens regardless of window size (cost and latency control).</summary>
    public int MaxInputTokens { get; init; } = 24_000;

    /// <summary>Share of the budget each source may use before others get leftovers. Unspecified sources get 0 guaranteed.</summary>
    public IReadOnlyDictionary<ContextSource, double> Allocation { get; init; } = new Dictionary<ContextSource, double>
    {
        [ContextSource.System] = 0.15,
        [ContextSource.LiveBusinessData] = 0.30,
        [ContextSource.ConversationHistory] = 0.25,
        [ContextSource.Memory] = 0.10,
        [ContextSource.Retrieval] = 0.20,
    };
}

/// <summary>Computes the input token budget and its per-source allocation.</summary>
public sealed class TokenBudgetManager(TokenBudgetOptions options)
{
    public TokenBudgetOptions Options { get; } = options;

    public int TotalInputBudget =>
        Math.Max(0, Math.Min(Options.MaxInputTokens, Options.ContextWindow - Options.ReservedForOutput - Options.ReservedForTools));

    public int AllocationFor(ContextSource source) =>
        (int)Math.Floor(TotalInputBudget * Options.Allocation.GetValueOrDefault(source));

    /// <summary>
    /// Cheap, provider-agnostic estimate (~4 characters per token, rounded up). Replace with the
    /// model tokenizer when exact counts matter.
    /// </summary>
    public static int EstimateTokens(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;
}
