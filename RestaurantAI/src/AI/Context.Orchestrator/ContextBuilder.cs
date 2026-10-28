namespace RestaurantAI.AI.Context;

/// <summary>Collects candidate context items for one model call.</summary>
public sealed class ContextBuilder
{
    private readonly List<ContextItem> _items = [];

    public IReadOnlyList<ContextItem> Items => _items;

    public ContextBuilder AddSystem(string id, string content) =>
        Add(new ContextItem(id, ContextSource.System, content, Relevance: 1, Pinned: true));

    public ContextBuilder AddLiveData(string id, string content, string? entityKey = null, DateTimeOffset? asOf = null) =>
        Add(new ContextItem(id, ContextSource.LiveBusinessData, content, Relevance: 1, Timestamp: asOf, EntityKey: entityKey));

    public ContextBuilder AddHistory(string id, string content, DateTimeOffset at, bool pinned = false) =>
        Add(new ContextItem(id, ContextSource.ConversationHistory, content, Relevance: 1, Timestamp: at, Pinned: pinned));

    public ContextBuilder AddMemory(string id, string content, double confidence, DateTimeOffset updatedAt) =>
        Add(new ContextItem(id, ContextSource.Memory, content, Relevance: confidence, Timestamp: updatedAt));

    public ContextBuilder AddRetrieved(string id, string content, double similarity, string? entityKey = null) =>
        Add(new ContextItem(id, ContextSource.Retrieval, content, Relevance: similarity, EntityKey: entityKey));

    public ContextBuilder Add(ContextItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Content))
        {
            _items.Add(item);
        }

        return this;
    }
}
