using GPTipsBot.Dtos;
using GPTipsBot.Services.Cache;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;

namespace GPTipsBotTests.Services;

[TestFixture]
public class VisionImageCacheTests
{
    [Test]
    public void Remember_ThenTryGet_ReturnsFileId()
    {
        var cache = new VisionImageCache(new MemoryCache(new MemoryCacheOptions()), TimeSpan.FromMinutes(5));
        var key = new UserChatKey(1, 2);

        cache.Remember(key, "file-1");

        Assert.That(cache.TryGet(key, out var fileId), Is.True);
        Assert.That(fileId, Is.EqualTo("file-1"));
    }

    [Test]
    public void Forget_RemovesFileId()
    {
        var cache = new VisionImageCache(new MemoryCache(new MemoryCacheOptions()), TimeSpan.FromMinutes(5));
        var key = new UserChatKey(1, 2);
        cache.Remember(key, "file-1");

        cache.Forget(key);

        Assert.That(cache.TryGet(key, out _), Is.False);
    }

    [Test]
    public void Remember_EmptyFileId_IsIgnored()
    {
        var cache = new VisionImageCache(new MemoryCache(new MemoryCacheOptions()), TimeSpan.FromMinutes(5));
        var key = new UserChatKey(1, 2);

        cache.Remember(key, " ");

        Assert.That(cache.TryGet(key, out _), Is.False);
    }
}
