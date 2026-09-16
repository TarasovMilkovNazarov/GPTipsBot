using FluentAssertions;
using GPTipsBot.Config;
using NUnit.Framework;

namespace GPTipsBotTests;

/// <summary>
/// The gem is an abstract unit with one published rate per payment rail. These lock in the three
/// properties that make that worth the trouble: prices are whole numbers, the unit is fine enough to
/// reprice without another redenomination, and no rail sells balance cheaper than another.
/// </summary>
[TestFixture]
public class GemPricingTests
{
    private static readonly (string Name, int Price)[] FeaturePrices =
    [
        (nameof(PaymentConfig.Gpt), PaymentConfig.Gpt),
        (nameof(PaymentConfig.Gpt5Mini), PaymentConfig.Gpt5Mini),
        (nameof(PaymentConfig.Gpt41), PaymentConfig.Gpt41),
        (nameof(PaymentConfig.Gpt5), PaymentConfig.Gpt5),
        (nameof(PaymentConfig.GptReasoning), PaymentConfig.GptReasoning),
        (nameof(PaymentConfig.Summary), PaymentConfig.Summary),
        (nameof(PaymentConfig.Image), PaymentConfig.Image),
        (nameof(PaymentConfig.Animation), PaymentConfig.Animation),
        (nameof(PaymentConfig.GptImageLow), PaymentConfig.GptImageLow),
        (nameof(PaymentConfig.GptImageMedium), PaymentConfig.GptImageMedium),
        (nameof(PaymentConfig.GptImageHigh), PaymentConfig.GptImageHigh),
        (nameof(PaymentConfig.WatermarkRemoval), PaymentConfig.WatermarkRemoval),
    ];

    [Test]
    public void EveryFeaturePrice_IsAPositiveWholeNumberOfGems()
    {
        foreach (var (name, price) in FeaturePrices)
        {
            price.Should().BePositive("{0} must cost something", name);
        }
    }

    /// <summary>
    /// The point of a sub-kopeck unit is headroom: the cheapest action has to be able to get much
    /// cheaper before the scale bottoms out and a redenomination is needed.
    /// </summary>
    [Test]
    public void CheapestAction_HasRoomToGetCheaper()
    {
        var cheapest = FeaturePrices.Min(p => p.Price);
        cheapest.Should().BeGreaterThanOrEqualTo(
            4,
            "a price that can only fall a handful of steps leaves no room to follow falling model costs");
    }

    [Test]
    public void ChatLadder_KeepsItsRelativeCosts()
    {
        PaymentConfig.Gpt5Mini.Should().Be(PaymentConfig.Gpt * 3);
        PaymentConfig.Gpt41.Should().Be(PaymentConfig.Gpt * 10);
        PaymentConfig.Gpt5.Should().Be(PaymentConfig.Gpt * 12);
        PaymentConfig.GptReasoning.Should().Be(PaymentConfig.Gpt * 20);
    }

    [Test]
    public void StickerPack_PricesFollowTheImageTiers()
    {
        StickerPackConfig.HeroGems.Should().Be(PaymentConfig.GptImageMedium);
        StickerPackConfig.VariantGems.Should().Be(PaymentConfig.GptImageLow);
        StickerPackConfig.PackRemainderGems.Should()
            .Be(StickerPackConfig.VariantGems * (StickerPackConfig.EmotionCount - 1));
    }

    /// <summary>
    /// Telegram pays out roughly 1 ₽ per XTR. If a star bought more gems than a ruble does, the Stars
    /// rail would sell balance below the fiat price — the exact hole that crediting XTR one-for-one
    /// against a 10 ₽ unit used to open.
    /// </summary>
    [Test]
    public void StarsRail_DoesNotUndercutTheFiatRail()
    {
        const int gemsPerRubOfPayout = 1;

        TelegramStarsConfig.GemsPerXtr.Should().BeLessThanOrEqualTo(
            YooKassaConfig.GemsPerRub * gemsPerRubOfPayout,
            "a star nets about a ruble, so it must not buy more gems than a ruble does");
    }
}
