using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace GPTipsBot.Services.Cache;

public class AliceImageSession
{
    public string? FirstFileId { get; set; }
    public string? SecondFileId { get; set; }
}

public interface IAliceImageSessionCache
{
    AliceImageSession GetOrCreate(long chatId);
    void Set(long chatId, AliceImageSession session);
    void Remove(long chatId);
}

public class AliceImageSessionCache(IMemoryCache cache, TimeSpan ttl) : IAliceImageSessionCache
{
    public AliceImageSession GetOrCreate(long chatId)
    {
        var key = Key(chatId);
        if (cache.TryGetValue(key, out AliceImageSession? session) && session != null)
        {
            return session;
        }

        session = new AliceImageSession();
        Set(chatId, session);
        return session;
    }

    public void Set(long chatId, AliceImageSession session)
    {
        cache.Set(Key(chatId), session, new MemoryCacheEntryOptions().SetSlidingExpiration(ttl));
    }

    public void Remove(long chatId) => cache.Remove(Key(chatId));

    private static string Key(long chatId) => $"alice-image:{chatId}";
}

public static class AliceImageSessionCacheExtensions
{
    public static IServiceCollection AddAliceImageSessionCache(this IServiceCollection services, TimeSpan? ttl = null)
    {
        services.AddMemoryCache();
        services.AddSingleton<IAliceImageSessionCache>(sp =>
            new AliceImageSessionCache(
                sp.GetRequiredService<IMemoryCache>(),
                ttl ?? TimeSpan.FromMinutes(10)));
        return services;
    }
}
