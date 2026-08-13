using dotenv.net;
using GPTipsBot.Db;
using GPTipsBot.Enums;
using GPTipsBot.Extensions;
using GPTipsBot.Models;
using GPTipsBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace GPTipsBotTests;

[TestFixture]
public class InferMissingUserLanguagesTests
{
    private IServiceProvider? _services;
    private bool _dbAvailable;
    private static readonly long[] TestUserIds = [902001, 902002, 902003, 902004, 902005, 902006, 902007];

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
    public async Task Run_InfersRuAndEn_DoesNotOverwriteExisting()
    {
        RequireDb();
        var db = _services!.GetRequiredService<ApplicationContext>();
        db.Users.AddRange(
            User(902001),
            User(902002),
            User(902003),
            User(902004),
            User(902005),
            User(902006),
            User(902007));
        db.BotSettings.AddRange(
            new BotSettings { Id = 902003, Language = "es" },
            new BotSettings { Id = 902005, Language = "" },
            new BotSettings { Id = 902006, Language = "en" },
            new BotSettings { Id = 902007, Language = "en" });
        db.Messages.AddRange(
            Msg(902001, "Привет, как дела?"),
            Msg(902001, "Нарисуй кота"),
            Msg(902001, "hello"),
            Msg(902002, "Hello there"),
            Msg(902002, "draw a cat"),
            Msg(902002, "write a python script"),
            Msg(902003, "Только русский текст, но язык уже задан"),
            Msg(902005, "Спасибо большое"),
            Msg(902006, "Сделай картинку кота"),
            Msg(902007, "draw a cat please"));
        await db.SaveChangesAsync();

        var job = _services.GetRequiredService<InferMissingUserLanguages>();
        var updated = await job.RunAsync(TestUserIds, CancellationToken.None);

        Assert.That(updated, Is.EqualTo(5));
        db.ChangeTracker.Clear();
        Assert.That(db.BotSettings.Single(s => s.Id == 902001).Language, Is.EqualTo("ru"));
        Assert.That(db.BotSettings.Single(s => s.Id == 902002).Language, Is.EqualTo("en"));
        Assert.That(db.BotSettings.Single(s => s.Id == 902003).Language, Is.EqualTo("es"));
        Assert.That(db.BotSettings.Single(s => s.Id == 902004).Language, Is.EqualTo("ru"));
        Assert.That(db.BotSettings.Single(s => s.Id == 902005).Language, Is.EqualTo("ru"));
        Assert.That(db.BotSettings.Single(s => s.Id == 902006).Language, Is.EqualTo("ru"));
        Assert.That(db.BotSettings.Single(s => s.Id == 902007).Language, Is.EqualTo("en"));
    }

    private static User User(long id) => new()
    {
        Id = id,
        TelegramId = id,
        FirstName = "Test",
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static Message Msg(long userId, string text) => new()
    {
        UserId = userId,
        ChatId = userId,
        Text = text,
        Role = MessageOwner.User,
        CreatedAt = DateTime.UtcNow,
        ContextBound = false,
    };

    private static async Task ClearAsync(ApplicationContext db)
    {
        await db.Messages.Where(m => TestUserIds.Contains(m.UserId)).ExecuteDeleteAsync();
        await db.BotSettings.Where(s => TestUserIds.Contains(s.Id)).ExecuteDeleteAsync();
        await db.Users.Where(u => TestUserIds.Contains(u.Id)).ExecuteDeleteAsync();
    }
}
