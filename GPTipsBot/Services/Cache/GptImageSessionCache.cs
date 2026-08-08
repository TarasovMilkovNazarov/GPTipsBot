using GPTipsBot.Config;
using Microsoft.Extensions.Caching.Memory;

namespace GPTipsBot.Services.Cache;

public enum GptImageMode
{
    Generate = 0,
    Edit = 1,
}

public sealed class GptImageSession
{
    public GptImageMode Mode { get; set; } = GptImageMode.Generate;
    public string Size { get; set; } = GptImageConfig.SizeSquare;
    public string Quality { get; set; } = GptImageConfig.QualityMedium;
    public string? ImageFileId { get; set; }

    public double StarsCost => GptImageConfig.PriceFor(Quality);
}

public interface IGptImageSessionCache
{
    GptImageSession GetOrCreate(long userId);
    void Set(long userId, GptImageSession session);
    void Remove(long userId);
}

public class GptImageSessionCache(IMemoryCache cache) : IGptImageSessionCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    private static string Key(long userId) => $"gpt-image-session:{userId}";

    public GptImageSession GetOrCreate(long userId)
    {
        if (cache.TryGetValue(Key(userId), out GptImageSession? existing) && existing != null)
        {
            return existing;
        }

        var session = new GptImageSession();
        Set(userId, session);
        return session;
    }

    public void Set(long userId, GptImageSession session)
    {
        cache.Set(Key(userId), session, new MemoryCacheEntryOptions
        {
            SlidingExpiration = Ttl,
        });
    }

    public void Remove(long userId) => cache.Remove(Key(userId));
}
