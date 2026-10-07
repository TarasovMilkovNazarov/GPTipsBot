using Microsoft.Extensions.Caching.Memory;

namespace GPTipsBot.Services.Cache;

public sealed class TryOnSession
{
    /// <summary>Full-height photo of the user, reused for every next item.</summary>
    public string? PersonFileId { get; set; }
}

public interface ITryOnSessionCache
{
    TryOnSession GetOrCreate(long userId);
    void Set(long userId, TryOnSession session);
    void Remove(long userId);
}

public class TryOnSessionCache(IMemoryCache cache) : ITryOnSessionCache
{
    // Long enough to try on a few items later the same day without re-sending the photo.
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private static string Key(long userId) => $"try-on-session:{userId}";

    public TryOnSession GetOrCreate(long userId)
    {
        if (cache.TryGetValue(Key(userId), out TryOnSession? existing) && existing != null)
        {
            return existing;
        }

        var session = new TryOnSession();
        Set(userId, session);
        return session;
    }

    public void Set(long userId, TryOnSession session)
    {
        cache.Set(Key(userId), session, new MemoryCacheEntryOptions
        {
            SlidingExpiration = Ttl,
        });
    }

    public void Remove(long userId) => cache.Remove(Key(userId));
}
