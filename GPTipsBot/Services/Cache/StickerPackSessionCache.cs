using GPTipsBot.Config;
using Microsoft.Extensions.Caching.Memory;

namespace GPTipsBot.Services.Cache;

public enum StickerPackStep
{
    AwaitingSource = 0,
    HeroReady = 1,
    PackReady = 2,
}

public sealed class StickerPackSession
{
    public StickerPackStep Step { get; set; } = StickerPackStep.AwaitingSource;
    public string? SourceFileId { get; set; }
    public string? Description { get; set; }
    public byte[]? HeroPng { get; set; }
    public List<StickerDraft> Stickers { get; set; } = [];
    public bool Busy { get; set; }

    public void Reset()
    {
        Step = StickerPackStep.AwaitingSource;
        SourceFileId = null;
        Description = null;
        HeroPng = null;
        Stickers = [];
        Busy = false;
    }
}

public interface IStickerPackSessionCache
{
    StickerPackSession GetOrCreate(long userId);
    void Set(long userId, StickerPackSession session);
    void Remove(long userId);
}

public class StickerPackSessionCache(IMemoryCache cache) : IStickerPackSessionCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    private static string Key(long userId) => $"sticker-pack-session:{userId}";

    public StickerPackSession GetOrCreate(long userId)
    {
        if (cache.TryGetValue(Key(userId), out StickerPackSession? existing) && existing != null)
        {
            return existing;
        }

        var session = new StickerPackSession();
        Set(userId, session);
        return session;
    }

    public void Set(long userId, StickerPackSession session)
    {
        cache.Set(Key(userId), session, new MemoryCacheEntryOptions
        {
            SlidingExpiration = Ttl,
        });
    }

    public void Remove(long userId) => cache.Remove(Key(userId));
}
