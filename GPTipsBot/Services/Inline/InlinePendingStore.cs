using System.Collections.Concurrent;

namespace GPTipsBot.Services.Inline;

/// <summary>
/// Short-lived store for inline result payloads (Telegram result id is max 64 bytes).
/// </summary>
public class InlinePendingStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, InlinePendingRequest> _items = new();

    public InlinePendingRequest Create(InlinePendingKind kind, long telegramUserId, string payload)
    {
        CleanupExpired();

        var request = new InlinePendingRequest
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = kind,
            TelegramUserId = telegramUserId,
            Payload = payload,
        };

        _items[request.Id] = request;
        return request;
    }

    public bool TryTake(string id, long telegramUserId, out InlinePendingRequest? request)
    {
        request = null;
        if (!_items.TryRemove(id, out var found))
        {
            return false;
        }

        if (found.TelegramUserId != telegramUserId || IsExpired(found))
        {
            return false;
        }

        request = found;
        return true;
    }

    private void CleanupExpired()
    {
        foreach (var pair in _items)
        {
            if (IsExpired(pair.Value))
            {
                _items.TryRemove(pair.Key, out _);
            }
        }
    }

    private static bool IsExpired(InlinePendingRequest request) =>
        DateTime.UtcNow - request.CreatedAtUtc > Ttl;
}
