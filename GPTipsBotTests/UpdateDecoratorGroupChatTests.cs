using GPTipsBot.Config;
using GPTipsBot.Dtos;
using GPTipsBot.Exceptions;
using NUnit.Framework;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GPTipsBotTests;

[TestFixture]
public class UpdateDecoratorGroupChatTests
{
    [SetUp]
    public void SetUp()
    {
        AppConfig.BotName = "GPTipsBot";
    }

    [Test]
    public void GroupUpdate_ReplyChatId_IsSourceChat()
    {
        var update = CreateGroupTextUpdate("/help@GPTipsBot");

        var decorator = new UpdateDecorator(update);

        Assert.That(decorator.ReplyChatId, Is.EqualTo(-100500));
        Assert.That(decorator.UserChatKey.ChatId, Is.EqualTo(-100500));
        Assert.That(decorator.UserChatKey.Id, Is.EqualTo(100));
    }

    [Test]
    public void GroupCommand_IsAcceptedAndAddressed()
    {
        var update = CreateGroupTextUpdate("/help@GPTipsBot");

        var decorator = new UpdateDecorator(update);

        Assert.That(decorator.IsGroupOrChannel, Is.True);
        Assert.That(decorator.IsCommand, Is.True);
        Assert.That(decorator.IsAddressedToBot, Is.True);
        Assert.That(decorator.UserChatKey.Id, Is.EqualTo(100));
        Assert.That(decorator.UserChatKey.ChatId, Is.EqualTo(-100500));
    }

    [Test]
    public void GroupMention_StripsBotUsernameFromText()
    {
        var text = "@GPTipsBot hello world";
        var update = CreateGroupTextUpdate(text, new[]
        {
            new MessageEntity
            {
                Type = MessageEntityType.Mention,
                Offset = 0,
                Length = "@GPTipsBot".Length
            }
        });

        var decorator = new UpdateDecorator(update);

        Assert.That(decorator.IsAddressedToBot, Is.True);
        Assert.That(decorator.Message.Text, Is.EqualTo("hello world"));
        Assert.That(decorator.IsCommand, Is.False);
    }

    [Test]
    public void GroupPlainText_IsNotAddressedToBot()
    {
        var update = CreateGroupTextUpdate("random group chatter");

        var decorator = new UpdateDecorator(update);

        Assert.That(decorator.IsGroupOrChannel, Is.True);
        Assert.That(decorator.IsAddressedToBot, Is.False);
    }

    [Test]
    public void GroupCommandForAnotherBot_IsIgnored()
    {
        var update = CreateGroupTextUpdate("/help@OtherBot");

        var decorator = new UpdateDecorator(update);

        Assert.That(decorator.IsCommand, Is.False);
        Assert.That(decorator.IsAddressedToBot, Is.False);
    }

    [Test]
    public void ReplyToBot_IsAddressed()
    {
        var update = CreateGroupTextUpdate("follow-up question");
        update.Message!.ReplyToMessage = new Message
        {
            Id = 50,
            Date = DateTime.UtcNow,
            Chat = update.Message.Chat,
            From = new User
            {
                Id = 1,
                IsBot = true,
                FirstName = "GPTipsBot",
                Username = "GPTipsBot"
            },
            Text = "previous answer"
        };

        var decorator = new UpdateDecorator(update);

        Assert.That(decorator.IsAddressedToBot, Is.True);
    }

    [Test]
    public void ChannelPost_IsIgnored()
    {
        var update = new Update
        {
            Id = 1,
            Message = new Message
            {
                Id = 1,
                Date = DateTime.UtcNow,
                From = new User { Id = 100, IsBot = false, FirstName = "User" },
                Chat = new Chat { Id = -100500, Type = ChatType.Channel, Title = "Channel" },
                Text = "/help"
            }
        };

        Assert.Throws<IgnoreMessageTypeException>(() => _ = new UpdateDecorator(update));
    }

    [Test]
    public void TryGetCommandArgument_SupportsBotUsernameSuffix()
    {
        var ok = UpdateDecorator.TryGetCommandArgument(
            "/image@GPTipsBot a cute cat",
            "/image",
            out var argument);

        Assert.That(ok, Is.True);
        Assert.That(argument, Is.EqualTo("a cute cat"));
    }

    private static Update CreateGroupTextUpdate(string text, MessageEntity[]? entities = null)
    {
        return new Update
        {
            Id = 1,
            Message = new Message
            {
                Id = 10,
                Date = DateTime.UtcNow,
                From = new User
                {
                    Id = 100,
                    IsBot = false,
                    FirstName = "Aleksandr",
                    Username = "alanextar",
                    LanguageCode = "ru"
                },
                Chat = new Chat
                {
                    Id = -100500,
                    Type = ChatType.Supergroup,
                    Title = "Test Group"
                },
                Text = text,
                Entities = entities
            }
        };
    }
}
