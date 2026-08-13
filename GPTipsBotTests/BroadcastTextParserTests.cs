using GPTipsBot.Services.Broadcast;
using NUnit.Framework;

namespace GPTipsBotTests;

[TestFixture]
public class BroadcastTextParserTests
{
    [Test]
    public void Parse_PlainText_UsesFallbackLanguage()
    {
        var texts = BroadcastTextParser.Parse("Привет всем", "ru");

        Assert.That(texts, Has.Count.EqualTo(1));
        Assert.That(texts["ru"], Is.EqualTo("Привет всем"));
    }

    [Test]
    public void Parse_LanguageBlocks_SplitsByHeader()
    {
        var raw = """
                  ru:
                  Привет

                  en:
                  Hello
                  """;

        var texts = BroadcastTextParser.Parse(raw);

        Assert.That(texts["ru"], Is.EqualTo("Привет"));
        Assert.That(texts["en"], Is.EqualTo("Hello"));
    }

    [Test]
    public void Parse_InlineHeader_KeepsRestOfLine()
    {
        var texts = BroadcastTextParser.Parse("en: Hello there\nru: Привет");

        Assert.That(texts["en"], Is.EqualTo("Hello there"));
        Assert.That(texts["ru"], Is.EqualTo("Привет"));
    }

    [Test]
    public void ResolveText_FallsBackWhenLanguageMissing()
    {
        var config = new BroadcastCampaignConfig
        {
            FallbackLanguage = "ru",
            Texts = new Dictionary<string, string>
            {
                ["ru"] = "Русский",
                ["en"] = "English",
            },
        };

        Assert.That(BroadcastTextParser.ResolveText(config, "es"), Is.EqualTo("Русский"));
        Assert.That(BroadcastTextParser.ResolveText(config, "en"), Is.EqualTo("English"));
        Assert.That(BroadcastTextParser.ResolveText(config, "fa"), Is.EqualTo("Русский"));
    }

    [Test]
    public void Validate_RejectsTooLongText()
    {
        var config = new BroadcastCampaignConfig
        {
            Texts = { ["ru"] = new string('a', BroadcastCampaignConfig.TelegramMaxMessageLength + 1) },
        };

        Assert.That(BroadcastTextParser.Validate(config), Does.Contain("4096"));
    }
}
