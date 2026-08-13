using dotenv.net;
using GPTipsBot.Db;
using GPTipsBot.Extensions;
using GPTipsBot.Models;
using GPTipsBot.Services.Broadcast;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace GPTipsBotTests;

[TestFixture]
public class BroadcastServiceTests
{
    private IServiceProvider? _services;
    private bool _dbAvailable;
    private static readonly long[] TestUserIds = [901001, 901002, 901003, 901004];

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        DotEnv.Fluent().WithProbeForEnv(10).Load();
        Environment.SetEnvironmentVariable("ConnectionString",
            "Server=localhost;Port=5434;Database=gptips;User Id=postgres;Password=postgres;");
    }

    [SetUp]
    public async Task SetUp()
    {
        try
        {
            _services = new ServiceCollection().ConfigureServices().BuildServiceProvider();
            var db = _services.GetRequiredService<ApplicationContext>();
            await ClearAsync(db);
            _dbAvailable = true;
        }
        catch (Exception)
        {
            _dbAvailable = false;
            _services = null;
        }
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_services != null)
        {
            await ClearAsync(_services.GetRequiredService<ApplicationContext>());
        }
    }

    private void RequireDb()
    {
        if (!_dbAvailable || _services is null)
        {
            Assert.Ignore("Postgres is not available on localhost:5434");
        }
    }

    [Test]
    public async Task CountByLanguage_UsersWithoutSettingsAndMixedCase_DoesNotThrow()
    {
        RequireDb();
        var db = _services!.GetRequiredService<ApplicationContext>();
        var service = _services.GetRequiredService<BroadcastService>();
        var before = await service.CountByLanguageAsync(new BroadcastCampaignConfig(), CancellationToken.None);

        db.Users.AddRange(
            User(901001, 901001),
            User(901002, 901002),
            User(901003, 901003),
            User(901004, null));
        db.BotSettings.AddRange(
            new BotSettings { Id = 901001, Language = "ru" },
            new BotSettings { Id = 901002, Language = "RU" });
        await db.SaveChangesAsync();

        var counts = await service.CountByLanguageAsync(new BroadcastCampaignConfig(), CancellationToken.None);
        before.TryGetValue("ru", out var ruBefore);

        Assert.That(counts["ru"], Is.EqualTo(ruBefore + 3));
        Assert.That(counts.Keys, Does.Not.Contain("RU"));
        Assert.That(await service.CountAudienceAsync(new BroadcastCampaignConfig(), CancellationToken.None),
            Is.EqualTo(before.Values.Sum() + 3));
    }

    [Test]
    public async Task TakeBatch_ReturnsTelegramIdAndNormalizedLanguage()
    {
        RequireDb();
        var db = _services!.GetRequiredService<ApplicationContext>();
        db.Users.Add(User(901001, 777001));
        db.BotSettings.Add(new BotSettings { Id = 901001, Language = "en-US" });
        await db.SaveChangesAsync();

        var service = _services.GetRequiredService<BroadcastService>();
        var batch = await service.TakeBatchAsync(new BroadcastCampaignConfig(), 901000, 50, CancellationToken.None);
        var row = batch.Single(r => r.UserId == 901001);

        Assert.That(row.TelegramId, Is.EqualTo(777001));
        Assert.That(row.Language, Is.EqualTo("en"));
    }

    private static User User(long id, long? telegramId) => new()
    {
        Id = id,
        TelegramId = telegramId,
        FirstName = "Test",
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static async Task ClearAsync(ApplicationContext db)
    {
        await db.BotSettings.Where(s => TestUserIds.Contains(s.Id)).ExecuteDeleteAsync();
        await db.Users.Where(u => TestUserIds.Contains(u.Id)).ExecuteDeleteAsync();
    }
}
