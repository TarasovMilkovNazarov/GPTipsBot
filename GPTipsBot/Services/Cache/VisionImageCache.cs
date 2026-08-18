using GPTipsBot.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace GPTipsBot.Services.Cache;

/// <summary>
/// Remembers the last photo a user sent in a chat so follow-up questions
/// ("does Yulia work tomorrow?") can still see the image.
/// </summary>
public interface IVisionImageCache
{
    void Remember(UserChatKey key, string fileId);
    bool TryGet(UserChatKey key, out string fileId);
    void Forget(UserChatKey key);
}

public class VisionImageCache(IMemoryCache cache, TimeSpan ttl) : IVisionImageCache
{
    public void Remember(UserChatKey key, string fileId)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return;
        }

        cache.Set(CacheKey(key), fileId, new MemoryCacheEntryOptions().SetSlidingExpiration(ttl));
    }

    public bool TryGet(UserChatKey key, out string fileId)
    {
        if (cache.TryGetValue(CacheKey(key), out string? id) && !string.IsNullOrWhiteSpace(id))
        {
            fileId = id;
            return true;
        }

        fileId = string.Empty;
        return false;
    }

    public void Forget(UserChatKey key) => cache.Remove(CacheKey(key));

    private static string CacheKey(UserChatKey key) => $"vision-image:{key.Id}:{key.ChatId}";
}

public static class VisionImageCacheServiceCollectionExtensions
{
    public static IServiceCollection AddVisionImageCache(this IServiceCollection services, TimeSpan? ttl = null)
    {
        services.AddMemoryCache();
        services.AddSingleton<IVisionImageCache>(sp =>
            new VisionImageCache(
                sp.GetRequiredService<IMemoryCache>(),
                ttl ?? TimeSpan.FromHours(2)));

        return services;
    }
}
