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
        // BuildDepositAmountKeyboard/IsLavaTopAmountAllowed treat a rail with no real price as usable.
        MoneyService.ToLavaTopAmount(1_000_000).Should().Be(decimal.MaxValue);
    }

    [Test]
    public void MinRechargeAmount_DefaultsToLavaTopsOwnUsdFloor()
    {
        // Not our own choice — lava.top's API itself rejects a USD invoice below $5
        // ("Amount=1.25 not in allowed limits=(5, 10000) for USD", confirmed live).
        LavaTopConfig.MinRechargeAmount.Should().Be(5m);
    }

    [Test]
    public void MaxRechargeAmount_DefaultsToLavaTopsOwnUsdCeiling()
    {
        LavaTopConfig.MaxRechargeAmount.Should().Be(10_000m);
    }

    [Test]
    public void IsLavaTopAmountAllowed_IsFalseWhenUnpriced()
    {
        // ToLavaTopAmount sentinel (decimal.MaxValue) must fail the upper bound too, not just look
        // affordable enough to clear the lower one.
        MoneyService.IsLavaTopAmountAllowed(1_000).Should().BeFalse();
    }

    [Test]
    public void Currency_DefaultsToUsd()
    {
        LavaTopConfig.Currency.Should().Be(CurrencyCode.Usd);
    }

    [Test]
    public void DepositPackages_IsEmptyWhenUnpriced()
    {
        // GemsPerUnit is 0 in the test process (no LAVATOP_GEMS_PER_UNIT set) — must degrade to an
        // empty list, not a package ladder priced at 0 gems per package.
        LavaTopConfig.DepositPackages.Should().BeEmpty();
    }
}
