using FluentAssertions;
using GPTipsBot.Config;
using GPTipsBot.Models;
using GPTipsBot.Services;
using NUnit.Framework;

namespace GPTipsBotTests;

/// <summary>
/// No env vars are set for lava.top in the test process, so <see cref="LavaTopConfig.GemsPerUnit"/> is
/// 0 here — these tests lock in that the "not priced yet" state degrades safely instead of dividing by
/// zero or looking artificially cheap.
/// </summary>
[TestFixture]
public class LavaTopConfigTests
{
    [Test]
    public void IsEnabled_IsFalseWithoutExplicitConfiguration()
    {
        LavaTopConfig.IsEnabled.Should().BeFalse(
            "no LAVATOP_API_KEY/OFFER_ID/GEMS_PER_UNIT is set for tests");
    }

    [Test]
    public void ToLavaTopAmount_NeverDividesByZeroWhenUnpriced()
    {
        var act = () => MoneyService.ToLavaTopAmount(1_000);
        act.Should().NotThrow();
    }

    [Test]
    public void ToLavaTopAmount_LooksUnaffordableWhenUnpriced()
    {
        // An unset rate must never look like it clears a min-amount check — that would let
        // BuildPaymentMethodKeyboard show a button for a rail with no real price.
        MoneyService.ToLavaTopAmount(1_000_000).Should().Be(decimal.MaxValue);
    }

    [Test]
    public void MinRechargeAmount_DefaultsToAPositiveAmount()
    {
        LavaTopConfig.MinRechargeAmount.Should().BePositive();
    }

    [Test]
    public void Currency_DefaultsToUsd()
    {
        LavaTopConfig.Currency.Should().Be(CurrencyCode.Usd);
    }
}
