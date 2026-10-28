namespace RestaurantAI.AI.Context;

/// <summary>
/// Scores context items: score = relevance × source weight × recency factor.
/// Authoritative live data outranks memory and retrieved knowledge.
/// </summary>
public sealed class ContextRankingEngine(TimeProvider timeProvider)
{
    public static readonly IReadOnlyDictionary<ContextSource, double> SourceWeights = new Dictionary<ContextSource, double>
    {
        [ContextSource.System] = 10.0,
        [ContextSource.LiveBusinessData] = 5.0,
        [ContextSource.ConversationHistory] = 2.0,
        [ContextSource.Memory] = 1.2,
        [ContextSource.Retrieval] = 1.0,
    };

    /// <summary>Half-life used for recency decay of time-stamped items.</summary>
    public TimeSpan HalfLife { get; init; } = TimeSpan.FromHours(6);

    public IReadOnlyList<RankedContextItem> Rank(IEnumerable<ContextItem> items)
    {
        var now = timeProvider.GetUtcNow();
        return items
            .Select(item => new RankedContextItem(item, Score(item, now)))
            .OrderByDescending(r => r.Item.Pinned)
            .ThenByDescending(r => r.Score)
            .ThenBy(r => r.Item.Tokens)
            .ToList();
    }

    public double Score(ContextItem item, DateTimeOffset now)
    {
        var relevance = Math.Clamp(item.Relevance, 0, 1);
        var weight = SourceWeights.GetValueOrDefault(item.Source, 1.0);
        return relevance * weight * RecencyFactor(item, now);
    }

    private double RecencyFactor(ContextItem item, DateTimeOffset now)
    {
        if (item.Timestamp is not { } timestamp || item.Source is ContextSource.System or ContextSource.LiveBusinessData)
        {
            return 1.0;
        }

        var age = now - timestamp;
        return age <= TimeSpan.Zero ? 1.0 : Math.Pow(0.5, age / HalfLife);
    }
}
