namespace GPTipsBot.Services.Broadcast;

public static class BroadcastCallbacks
{
    public const string Prefix = "bc:";
    public const string Preview = "bc:preview";
    public const string Test = "bc:test";
    public const string Start = "bc:start";
    public const string Stop = "bc:stop";
    public const string Cancel = "bc:cancel";
    public const string Status = "bc:status";

    public static bool IsMatch(string? data) =>
        !string.IsNullOrEmpty(data) && data.StartsWith(Prefix, StringComparison.Ordinal);
}

public sealed class BroadcastDraft
{
    public BroadcastCampaignConfig Config { get; set; } = new();
    public bool AwaitingText { get; set; }
    public long? CampaignId { get; set; }
}

public sealed class BroadcastDraftStore
{
    private readonly Dictionary<long, BroadcastDraft> _drafts = new();
    private readonly object _gate = new();

    public BroadcastDraft GetOrCreate(long adminTelegramId)
    {
        lock (_gate)
        {
            if (!_drafts.TryGetValue(adminTelegramId, out var draft))
            {
                draft = new BroadcastDraft();
                _drafts[adminTelegramId] = draft;
            }

            return draft;
        }
    }

    public BroadcastDraft? Get(long adminTelegramId)
    {
        lock (_gate)
        {
            return _drafts.TryGetValue(adminTelegramId, out var draft) ? draft : null;
        }
    }

    public bool IsAwaitingText(long adminTelegramId)
    {
        lock (_gate)
        {
            return _drafts.TryGetValue(adminTelegramId, out var draft) && draft.AwaitingText;
        }
    }

    public void Clear(long adminTelegramId)
    {
        lock (_gate)
        {
            _drafts.Remove(adminTelegramId);
        }
    }
}
