using GPTipsBot.Config;
using GPTipsBot.Services;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GPTipsBotTests;

[TestFixture]
public class StickerPackTests
{
    [TestCase("сделай стикеры", null)]
    [TestCase("создай стикерпак", null)]
    [TestCase("сделай стикеры из фото", null)]
    [TestCase("сделай стикеры кота в шляпе", "кота в шляпе")]
    [TestCase("create a sticker pack of a red panda", "a red panda")]
    [TestCase("sticker pack", null)]
    [TestCase("stickers", null)]
    [TestCase("стикеры", null)]
    public void TryMatch_StickerPack(string text, string? prompt)
    {
        var route = NaturalLanguageToolRouter.TryMatch(text, hasPhoto: false);
        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.StickerPack));
        Assert.That(route.Prompt, Is.EqualTo(prompt));
    }

    [Test]
    public void TryMatch_DrawGiraffe_IsNotStickerPack()
    {
        var route = NaturalLanguageToolRouter.TryMatch("нарисуй жирафа", hasPhoto: false);
        Assert.That(route.Intent, Is.EqualTo(MediaToolIntent.GenerateImage));
    }

    [Test]
    public void BuildSetName_EndsWithByBot_AndFitsLimit()
    {
        var name = StickerPackConfig.BuildSetName(486363646, "GPTipsBot");
        Assert.That(name, Does.StartWith("stkr486363646_"));
        Assert.That(name, Does.EndWith("_by_GPTipsBot").IgnoreCase);
        Assert.That(name.Length, Is.LessThanOrEqualTo(64));
        Assert.That(name, Does.Not.Contain("__"));
    }

    [Test]
    public void BuildSetName_StripsInvalidCharacters()
    {
        var name = StickerPackConfig.BuildSetName(1, "@My-Bot!");
        Assert.That(name, Does.EndWith("_by_MyBot"));
        Assert.That(name.Length, Is.LessThanOrEqualTo(64));
    }

    [Test]
    public void ToTelegramPng_ResizesSoOneSideIs512()
    {
        using var source = new Image<Rgba32>(1024, 1024);
        using var input = new MemoryStream();
        source.SaveAsPng(input);
        var png = StickerImageProcessor.ToTelegramPng(input.ToArray());

        using var result = Image.Load(png);
        Assert.That(result.Width, Is.EqualTo(512));
        Assert.That(result.Height, Is.EqualTo(512));
    }

    [Test]
    public void ToTelegramPng_Landscape_KeepsAspectAndHits512()
    {
        using var source = new Image<Rgba32>(1536, 1024);
        using var input = new MemoryStream();
        source.SaveAsPng(input);
        var png = StickerImageProcessor.ToTelegramPng(input.ToArray());

        using var result = Image.Load(png);
        Assert.That(result.Width, Is.EqualTo(512));
        Assert.That(result.Height, Is.LessThanOrEqualTo(512));
        Assert.That(result.Width == 512 || result.Height == 512, Is.True);
    }
}
