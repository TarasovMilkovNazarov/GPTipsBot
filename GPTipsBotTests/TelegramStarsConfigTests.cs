using FluentAssertions;
using GPTipsBot.Config;
using GPTipsBot.Services;
using NUnit.Framework;

namespace GPTipsBotTests;

[TestFixture]
public class TelegramStarsConfigTests
{
    [TestCase(20, 1)]
    [TestCase(1_000, 50)]
    [TestCase(20_000, 1_000)]
    public void GemsToXtr_ConvertsAtTheDeclaredRate(int gems, int expectedXtr)
    {
        TelegramStarsConfig.GemsToXtr(gems).Should().Be(expectedXtr);
    }

    [TestCase(1, 1)]
    [TestCase(19, 1)]
    [TestCase(21, 2)]
    [TestCase(1_001, 51)]
    public void GemsToXtr_RoundsUpBecauseAStarCannotBeSplit(int gems, int expectedXtr)
    {
        TelegramStarsConfig.GemsToXtr(gems).Should().Be(
            expectedXtr,
            "rounding down would hand out gems the payer never covered");
    }

    [Test]
    public void DepositPackages_AreWholeNumbersOfStars()
    {
        foreach (var gems in PaymentConfig.DepositGemPackages)
        {
            (gems % TelegramStarsConfig.GemsPerXtr).Should().Be(
                0,
                "package {0}💎 is offered on the Stars rail, so it must not need rounding",
                gems);
        }
    }

    [Test]
    public void DepositPackages_FitIntoASingleTelegramInvoice()
    {
        foreach (var gems in PaymentConfig.DepositGemPackages)
        {
            TelegramStarsConfig.GemsToXtr(gems).Should().BeLessThanOrEqualTo(
                TelegramStarsConfig.MaxXtrPerInvoice,
                "Telegram rejects invoices above {0} stars",
                TelegramStarsConfig.MaxXtrPerInvoice);
        }
    }

    [Test]
    public void MaxGemsPerInvoice_StaysUnderTelegramCap()
    {
        TelegramStarsConfig.GemsToXtr(TelegramStarsConfig.MaxGemsPerInvoice)
            .Should().BeLessThanOrEqualTo(TelegramStarsConfig.MaxXtrPerInvoice);
    }

    [Test]
    public void MinRechargeGems_IsStillPayableWithStars()
    {
        ((long)PaymentConfig.MinRechargeGems).Should()
            .BeLessThanOrEqualTo(TelegramStarsConfig.MaxGemsPerInvoice);
    }

    [Test]
    public void MinRechargeGems_CoversTheRubleMinimum()
    {
        MoneyService.ToKopecks(PaymentConfig.MinRechargeGems)
            .Should().BeGreaterThanOrEqualTo(PaymentConfig.MinRechargeRub * 100L);
    }
}
