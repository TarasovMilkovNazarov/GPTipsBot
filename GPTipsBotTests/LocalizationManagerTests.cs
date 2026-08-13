using GPTipsBot.Localization;
using NUnit.Framework;

namespace GPTipsBotTests;

[TestFixture]
public class LocalizationManagerTests
{
    [TestCase("ru", "ru")]
    [TestCase("ru-RU", "ru")]
    [TestCase("ru_RU", "ru")]
    [TestCase("uk", "ru")]
    [TestCase("be", "ru")]
    [TestCase("kk", "ru")]
    [TestCase("ua", "ru")]
    [TestCase("by", "ru")]
    [TestCase("en", "en")]
    [TestCase("en-US", "en")]
    [TestCase("de", "en")]
    [TestCase("fr", "en")]
    [TestCase("es", "es")]
    [TestCase("fa", "fa")]
    [TestCase("ar", "ar")]
    public void NormalizeLanguage_MapsTelegramAndCisCodes(string input, string expected)
    {
        Assert.That(LocalizationManager.NormalizeLanguage(input), Is.EqualTo(expected));
    }

    [Test]
    public void DatabaseTagsFor_Ru_IncludesTelegramCisCodes()
    {
        var tags = LocalizationManager.DatabaseTagsFor("ru");

        Assert.That(tags, Does.Contain("ru"));
        Assert.That(tags, Does.Contain("uk"));
        Assert.That(tags, Does.Contain("be"));
        Assert.That(tags, Does.Contain("kk"));
    }
}
