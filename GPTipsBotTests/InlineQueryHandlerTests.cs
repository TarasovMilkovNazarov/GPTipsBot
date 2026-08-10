using GPTipsBot.Config;
using GPTipsBot.Dtos;
using GPTipsBot.Services.Inline;
using GPTipsBot.UpdateHandlers;
using NUnit.Framework;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GPTipsBotTests;

[TestFixture]
public class InlineQueryHandlerTests
{
    [SetUp]
    public void SetUp()
    {
        AppConfig.BotName = "GPTipsBot";
    }

    [TestCase("ask How does photosynthesis work?", "ask", "How does photosynthesis work?")]
    [TestCase("/ask How does photosynthesis work?", "ask", "How does photosynthesis work?")]
    [TestCase("ASK Hello", "ask", "Hello")]
    [TestCase("image a cat in space", "image", "a cat in space")]
    [TestCase("/image a cat in space", "image", "a cat in space")]
    public void TryParse_WithArgument_Succeeds(string text, string command, string expected)
    {
        Assert.That(InlineQueryHandler.TryParse(text, command, out var argument), Is.True);
        Assert.That(argument, Is.EqualTo(expected));
    }

    [TestCase("ask")]
    [TestCase("/ask")]
    [TestCase("ask ")]
    [TestCase("image")]
    [TestCase("hello world")]
    [TestCase("")]
    public void TryParse_WithoutArgument_Fails(string text)
    {
        Assert.That(InlineQueryHandler.TryParse(text, "ask", out _), Is.False);
        Assert.That(InlineQueryHandler.TryParse(text, "image", out _), Is.False);
    }

    [Test]
    public void UpdateDecorator_AcceptsInlineQuery()
    {
        var update = new Update
        {
            Id = 1,
            InlineQuery = new InlineQuery
            {
                Id = "q1",
                Query = "ask hello",
                From = new User
                {
                    Id = 42,
                    IsBot = false,
                    FirstName = "Ada",
                    LanguageCode = "en"
                },
                Offset = "",
                ChatType = ChatType.Sender
            }
        };

        var decorator = new UpdateDecorator(update);

        Assert.That(decorator.IsInline, Is.True);
        Assert.That(decorator.InlineQuery, Is.Not.Null);
        Assert.That(decorator.TelegramUserId, Is.EqualTo(42));
        Assert.That(decorator.UserChatKey.ChatId, Is.EqualTo(42));
        Assert.That(decorator.Language, Is.EqualTo("en"));
        Assert.That(decorator.IsGroupOrChannel, Is.False);
    }

    [Test]
    public void UpdateDecorator_AcceptsChosenInlineResult()
    {
        var update = new Update
        {
            Id = 2,
            ChosenInlineResult = new ChosenInlineResult
            {
                ResultId = "abc",
                From = new User
                {
                    Id = 7,
                    IsBot = false,
                    FirstName = "Bob",
                    LanguageCode = "ru"
                },
                Query = "image cat",
                InlineMessageId = "inline-msg-1"
            }
        };

        var decorator = new UpdateDecorator(update);

        Assert.That(decorator.IsInline, Is.True);
        Assert.That(decorator.ChosenInlineResult, Is.Not.Null);
        Assert.That(decorator.ChosenInlineResult!.InlineMessageId, Is.EqualTo("inline-msg-1"));
        Assert.That(decorator.TelegramUserId, Is.EqualTo(7));
    }

    [Test]
    public void InlinePendingStore_CreateAndTake_WorksOnce()
    {
        var store = new InlinePendingStore();
        var created = store.Create(InlinePendingKind.Ask, 10, "hello");

        Assert.That(store.TryTake(created.Id, 10, out var taken), Is.True);
        Assert.That(taken!.Payload, Is.EqualTo("hello"));
        Assert.That(store.TryTake(created.Id, 10, out _), Is.False);
    }

    [Test]
    public void InlinePendingStore_RejectsOtherUser()
    {
        var store = new InlinePendingStore();
        var created = store.Create(InlinePendingKind.Image, 10, "cat");

        Assert.That(store.TryTake(created.Id, 99, out _), Is.False);
    }
}
