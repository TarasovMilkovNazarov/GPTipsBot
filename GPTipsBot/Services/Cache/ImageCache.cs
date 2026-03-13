using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace GPTipsBot.Services.Cache;

public interface IImageCache
{
    void Set(long chatId, string imageId);
    bool TryGet(long chatId, out string? imageId);
    string? Get(long chatId);
    void Remove(long chatId);
}

public class ImageCache : IImageCache
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public ImageCache(IMemoryCache memoryCache, TimeSpan ttl)
    {
        _cache = memoryCache;
        _ttl = ttl;
    }

    public void Set(long chatId, string imageId)
    {
        var options = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(_ttl);

        _cache.Set(chatId, imageId, options);
    }

    public bool TryGet(long chatId, out string? imageId)
    {
        if (_cache.TryGetValue(chatId, out object value) && value is string id)
        {
            imageId = id;
            return true;
        }

        imageId = null;
        return false;
    }

    public string? Get(long chatId)
    {
        return TryGet(chatId, out var imageId) ? imageId : null;
    }

    public void Remove(long chatId)
    {
        _cache.Remove(chatId);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddImageCache(this IServiceCollection services, TimeSpan? ttl = null)
    {
        services.AddMemoryCache();
        services.AddSingleton<IImageCache>(sp =>
            new ImageCache(
                sp.GetRequiredService<IMemoryCache>(),
                ttl ?? TimeSpan.FromMinutes(5)
            ));

        return services;
    }
}