using GPTipsBot.Services.YandexCloud;
using NUnit.Framework;

namespace GPTipsBotTests;

[TestFixture]
public class YandexBillingBalanceAlertsTests
{
    [Test]
    public void NewlyCrossed_PositiveBalance_Empty()
    {
        var crossed = YandexBillingBalanceAlerts.NewlyCrossed(9.81m, new HashSet<decimal>());

        Assert.That(crossed, Is.Empty);
    }

    [Test]
    public void NewlyCrossed_BelowZero_OnlyZero()
    {
        var crossed = YandexBillingBalanceAlerts.NewlyCrossed(-1m, new HashSet<decimal>());

        Assert.That(crossed, Is.EqualTo(new[] { 0m }));
    }

    [Test]
    public void NewlyCrossed_BelowMinus2000_AllThree()
    {
        var crossed = YandexBillingBalanceAlerts.NewlyCrossed(-2000.01m, new HashSet<decimal>());

        Assert.That(crossed, Is.EqualTo(new[] { 0m, -1000m, -2000m }));
    }

    [Test]
    public void NewlyCrossed_AlreadyNotified_DoesNotRepeat()
    {
        var crossed = YandexBillingBalanceAlerts.NewlyCrossed(
            -1500m,
            new HashSet<decimal> { 0m, -1000m });

        Assert.That(crossed, Is.Empty);
    }

    [Test]
    public void Recovered_BalanceBackAboveZero_ClearsZero()
    {
        var recovered = YandexBillingBalanceAlerts.Recovered(10m, new HashSet<decimal> { 0m, -1000m });

        Assert.That(recovered, Is.EquivalentTo(new[] { 0m, -1000m }));
    }

    [Test]
    public void FormatMessage_SingleThreshold()
    {
        var text = YandexBillingBalanceAlerts.FormatMessage("alexeynazarov", -12.5m, [0m]);

        Assert.That(text, Does.Contain("«alexeynazarov» -12,5 ₽"));
        Assert.That(text, Does.Contain("Сработал порог: ниже 0 ₽"));
    }
}
