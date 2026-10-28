namespace RestaurantAI.AI.Context;

public sealed record EvictionResult(IReadOnlyList<ContextItem> Included, IReadOnlyList<EvictedContextItem> Evicted, int TokensUsed);

/// <summary>
/// Chooses which ranked items fit the token budget.
/// <list type="number">
/// <item>Retrieved knowledge about an entity that also has live business data is dropped (stale RAG never overrides authoritative data).</item>
/// <item>Pinned items are always kept.</item>
/// <item>Each source fills its guaranteed allocation in score order.</item>
/// <item>Remaining budget is shared by the best remaining items of any source.</item>
/// </list>
/// </summary>
public sealed class ContextEvictionPolicy(TokenBudgetManager budget)
{
    public EvictionResult Apply(IReadOnlyList<RankedContextItem> ranked)
    {
        var evicted = new List<EvictedContextItem>();
        var liveKeys = ranked
            .Where(r => r.Item.Source == ContextSource.LiveBusinessData && r.Item.EntityKey is not null)
            .Select(r => r.Item.EntityKey!)
            .ToHashSet(StringComparer.Ordinal);

        var candidates = new List<RankedContextItem>(ranked.Count);
        foreach (var entry in ranked)
        {
            if (entry.Item.Source == ContextSource.Retrieval && entry.Item.EntityKey is { } key && liveKeys.Contains(key))
            {
                evicted.Add(new EvictedContextItem(entry.Item, "superseded-by-live-data"));
            }
            else
            {
                candidates.Add(entry);
            }
        }

        var total = budget.TotalInputBudget;
        var included = new List<ContextItem>();
        var used = 0;
        var usedBySource = new Dictionary<ContextSource, int>();

        // 1. Pinned items.
        foreach (var entry in candidates.Where(c => c.Item.Pinned))
        {
            included.Add(entry.Item);
            used += entry.Item.Tokens;
            usedBySource[entry.Item.Source] = usedBySource.GetValueOrDefault(entry.Item.Source) + entry.Item.Tokens;
        }

        var remaining = candidates.Where(c => !c.Item.Pinned).ToList();
        var deferred = new List<RankedContextItem>();

        // 2. Guaranteed allocation per source.
        foreach (var entry in remaining)
        {
            var source = entry.Item.Source;
            var sourceUsed = usedBySource.GetValueOrDefault(source);
            if (sourceUsed + entry.Item.Tokens <= budget.AllocationFor(source) && used + entry.Item.Tokens <= total)
            {
                included.Add(entry.Item);
                used += entry.Item.Tokens;
                usedBySource[source] = sourceUsed + entry.Item.Tokens;
            }
            else
            {
                deferred.Add(entry);
            }
        }

        // 3. Shared leftover budget, best score first.
        foreach (var entry in deferred.OrderByDescending(d => d.Score))
        {
            if (used + entry.Item.Tokens <= total)
            {
                included.Add(entry.Item);
                used += entry.Item.Tokens;
            }
            else
            {
                evicted.Add(new EvictedContextItem(entry.Item, "token-budget"));
            }
        }

        // Preserve ranking order for the final prompt.
        var order = new Dictionary<ContextItem, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < ranked.Count; i++)
        {
            order[ranked[i].Item] = i;
        }

        included.Sort((a, b) => order[a].CompareTo(order[b]));

        return new EvictionResult(included, evicted, used);
    }
}
