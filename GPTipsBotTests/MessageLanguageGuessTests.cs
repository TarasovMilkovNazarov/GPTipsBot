using GPTipsBot.Localization;
using NUnit.Framework;

namespace GPTipsBotTests;

[TestFixture]
public class MessageLanguageGuessTests
{
    [Test]
    public void FromUserTexts_MajorityCyrillic_ReturnsRu()
    {
        var language = MessageLanguageGuess.FromUserTexts(
        [
            "Привет, как дела?",
            "Сделай картинку кота",
            "ok",
        ]);

        Assert.That(language, Is.EqualTo("ru"));
    }

    [Test]
    public void FromUserTexts_MajorityLatin_ReturnsEn()
    {
        var language = MessageLanguageGuess.FromUserTexts(
        [
            "Hello there",
            "draw a cat please",
            "Привет",
        ]);

        Assert.That(language, Is.EqualTo("en"));
    }

    [Test]
    public void FromUserTexts_TieOrEmpty_ReturnsEn()
    {
        Assert.That(MessageLanguageGuess.FromUserTexts(["Hello", "Привет"]), Is.EqualTo("en"));
        Assert.That(MessageLanguageGuess.FromUserTexts([]), Is.EqualTo("en"));
        Assert.That(MessageLanguageGuess.FromUserTexts(["/start", "👍", ""]), Is.EqualTo("en"));
    }

    [Test]
    public void ClassifyMessage_SkipsCommandsAndNonLetters()
    {
        Assert.That(MessageLanguageGuess.ClassifyMessage("/start"), Is.EqualTo(MessageLanguageVote.Skip));
        Assert.That(MessageLanguageGuess.ClassifyMessage("  /help me"), Is.EqualTo(MessageLanguageVote.Skip));
        Assert.That(MessageLanguageGuess.ClassifyMessage("🔥"), Is.EqualTo(MessageLanguageVote.Skip));
        Assert.That(MessageLanguageGuess.ClassifyMessage("Привет!"), Is.EqualTo(MessageLanguageVote.Russian));
        Assert.That(MessageLanguageGuess.ClassifyMessage("hello"), Is.EqualTo(MessageLanguageVote.Other));
        Assert.That(MessageLanguageGuess.ClassifyMessage("ok, спасибо"), Is.EqualTo(MessageLanguageVote.Russian));
    }
}
