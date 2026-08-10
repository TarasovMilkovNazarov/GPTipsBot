using GPTipsBot.Config;
using GPTipsBot.Services;
using NUnit.Framework;

namespace GPTipsBotTests;

[TestFixture]
public class AccountLinkTokenServiceTests
{
    private string? _previousToken;

    [SetUp]
    public void SetUp()
    {
        _previousToken = Environment.GetEnvironmentVariable("TELEGRAM_TOKEN");
        Environment.SetEnvironmentVariable("TELEGRAM_TOKEN", "test-bot-token-for-hmac");
        // AppConfig.TelegramToken reads env each time via GetEnvStrict — force property cache if any.
        // TelegramToken is a computed property, no cache.
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("TELEGRAM_TOKEN", _previousToken);
    }

    [Test]
    public void Create_And_Validate_RoundTrip()
    {
        var service = new AccountLinkTokenService();
        var token = service.Create(1_000_000_000_001);

        Assert.That(token, Does.StartWith(AccountLinkTokenService.Prefix));
        Assert.That(token.Length, Is.LessThanOrEqualTo(64));
        Assert.That(service.TryValidate(token, out var userId), Is.True);
        Assert.That(userId, Is.EqualTo(1_000_000_000_001));
    }

    [Test]
    public void TryValidate_Rejects_TamperedToken()
    {
        var service = new AccountLinkTokenService();
        var token = service.Create(42);
        var chars = token.ToCharArray();
        chars[^1] = chars[^1] == 'A' ? 'B' : 'A';
        var tampered = new string(chars);

        Assert.That(service.TryValidate(tampered, out _), Is.False);
    }

    [Test]
    public void TryValidate_Rejects_ExpiredToken()
    {
        var service = new AccountLinkTokenService();
        var token = service.Create(99, TimeSpan.FromSeconds(-10));

        Assert.That(service.TryValidate(token, out _), Is.False);
    }

    [Test]
    public void TryValidate_Rejects_WrongPrefix()
    {
        var service = new AccountLinkTokenService();
        Assert.That(service.IsLinkToken("referral_123"), Is.False);
        Assert.That(service.TryValidate("referral_123", out _), Is.False);
    }

    [Test]
    public void Create_Rejects_NonPositiveUserId()
    {
        var service = new AccountLinkTokenService();
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Create(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Create(-1));
    }
}
