using dotenv.net;
using GPTipsBot.Db;
using GPTipsBot.Extensions;
using GPTipsBot.Models;
using GPTipsBot.Services;
using GPTipsBot.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace GPTipsBotTests;

[TestFixture]
public class LinkTelegramIdToUserTests
{
    private IServiceProvider _services = null!;
    private string? _previousToken;

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
        _previousToken = Environment.GetEnvironmentVariable("TELEGRAM_TOKEN");
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TELEGRAM_TOKEN")))
        {
            Environment.SetEnvironmentVariable("TELEGRAM_TOKEN", "test-bot-token-for-hmac");
        }

        var services = new ServiceCollection().ConfigureServices();
        _services = services.BuildServiceProvider();
        var db = _services.GetRequiredService<ApplicationContext>();
        await ClearUsersAsync(db);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("TELEGRAM_TOKEN", _previousToken);
    }

    [Test]
    public async Task Link_AttachesTelegramId_ToEmailUser()
    {
        var users = _services.GetRequiredService<UserService>();
        var db = _services.GetRequiredService<ApplicationContext>();

        var emailUser = new User
        {
            Id = 1_000_000_000_101,
            FirstName = "Mail",
            Email = "link-test@example.com",
            EmailConfirmed = true,
            Source = WebAuthConstants.EmailSource,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true,
        };
        db.Users.Add(emailUser);
        await db.SaveChangesAsync();

        var linked = await users.LinkTelegramIdToUserAsync(emailUser.Id, 555001, "Tg", "Name");

        Assert.That(linked.Id, Is.EqualTo(emailUser.Id));
        Assert.That(linked.TelegramId, Is.EqualTo(555001));
        Assert.That(linked.FirstName, Is.EqualTo("Tg"));
    }

    [Test]
    public async Task Link_MergesExistingTelegramAccount_IntoEmail()
    {
        var users = _services.GetRequiredService<UserService>();
        var db = _services.GetRequiredService<ApplicationContext>();

        var emailUser = new User
        {
            Id = 1_000_000_000_102,
            FirstName = "Mail",
            Email = "merge-test@example.com",
            EmailConfirmed = true,
            Source = WebAuthConstants.EmailSource,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true,
            FreeGptRequests = 3,
        };
        var tgUser = new User
        {
            Id = 555002,
            TelegramId = 555002,
            FirstName = "TgOnly",
            Source = WebAuthConstants.TelegramSource,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true,
            FreeGptRequests = 7,
        };
        db.Users.Add(emailUser);
        db.Users.Add(tgUser);
        await db.SaveChangesAsync();

        db.Wallets.Add(new Wallet
        {
            UserId = 555002,
            Balance = 2.5,
            Currency = "XTR",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var linked = await users.LinkTelegramIdToUserAsync(emailUser.Id, 555002);

        Assert.That(linked.Id, Is.EqualTo(emailUser.Id));
        Assert.That(linked.TelegramId, Is.EqualTo(555002));
        Assert.That(linked.FreeGptRequests, Is.EqualTo(10));

        db.ChangeTracker.Clear();
        var loser = await db.Users.AsNoTracking().FirstAsync(u => u.Id == 555002);
        Assert.That(loser.IsActive, Is.False);
        Assert.That(loser.TelegramId, Is.Null);

        var survivorWallet = await db.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == emailUser.Id);
        Assert.That(survivorWallet?.Balance, Is.EqualTo(2.5));
    }

    [Test]
    public async Task Link_Rejects_WhenEmailAlreadyLinkedToOtherTelegram()
    {
        var users = _services.GetRequiredService<UserService>();
        var db = _services.GetRequiredService<ApplicationContext>();

        var emailUser = new User
        {
            Id = 1_000_000_000_103,
            FirstName = "Mail",
            Email = "conflict@example.com",
            EmailConfirmed = true,
            TelegramId = 111,
            Source = WebAuthConstants.EmailSource,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true,
        };
        db.Users.Add(emailUser);
        await db.SaveChangesAsync();

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await users.LinkTelegramIdToUserAsync(emailUser.Id, 222));
    }

    private static async Task ClearUsersAsync(ApplicationContext db)
    {
        var ids = new long[] { 1_000_000_000_101, 1_000_000_000_102, 1_000_000_000_103, 555001, 555002 };
        await db.PaymentHolds.Where(h => ids.Contains(h.UserId)).ExecuteDeleteAsync();
        await db.Invoices.Where(i => ids.Contains(i.UserId)).ExecuteDeleteAsync();
        await db.UserCommands.Where(c => ids.Contains(c.UserId)).ExecuteDeleteAsync();
        await db.AuthLoginEvents.Where(e => ids.Contains(e.UserId)).ExecuteDeleteAsync();
        await db.ConversationMetas.Where(m => ids.Contains(m.UserId)).ExecuteDeleteAsync();
        await db.Messages.Where(m => ids.Contains(m.UserId) || ids.Contains(m.ChatId)).ExecuteDeleteAsync();
        await db.BotSettings.Where(s => ids.Contains(s.Id)).ExecuteDeleteAsync();
        await db.Wallets.Where(w => ids.Contains(w.UserId)).ExecuteDeleteAsync();
        await db.Users.Where(u => ids.Contains(u.Id)).ExecuteDeleteAsync();
    }
}
