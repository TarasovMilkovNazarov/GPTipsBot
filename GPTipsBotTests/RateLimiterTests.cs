using GPTipsBot.Dtos;
using GPTipsBot.Services;
using NUnit.Framework;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GPTipsBotTests;

[TestFixture]
public class RateLimiterTests
{
    private RateLimiter _gate = null!;

    [SetUp]
    public void SetUp()
    {
        _gate = new RateLimiter();
    }

    [Test]
    public void TryEnter_FirstMessage_ProcessNow()
    {
        var update = CreatePrivateTextUpdate(1, "hello");

        Assert.That(_gate.TryEnter(update), Is.EqualTo(ChatGateResult.ProcessNow));
    }

    [Test]
    public void TryEnter_WhileBusy_QueuesThenDrops()
    {
        var first = CreatePrivateTextUpdate(1, "one");
        var second = CreatePrivateTextUpdate(2, "two");
        var third = CreatePrivateTextUpdate(3, "three");

        Assert.That(_gate.TryEnter(first), Is.EqualTo(ChatGateResult.ProcessNow));
        Assert.That(_gate.TryEnter(second), Is.EqualTo(ChatGateResult.Queued));
        Assert.That(_gate.TryEnter(third), Is.EqualTo(ChatGateResult.Dropped));
    }

    [Test]
    public void Release_ReturnsPendingThenFreesSlot()
    {
        var first = CreatePrivateTextUpdate(1, "one");
        var second = CreatePrivateTextUpdate(2, "two");

        _gate.TryEnter(first);
        _gate.TryEnter(second);

        var pending = _gate.Release(first.UserChatKey.ChatId);
        Assert.That(pending, Is.SameAs(second));

        Assert.That(_gate.Release(first.UserChatKey.ChatId), Is.Null);
        Assert.That(_gate.TryEnter(CreatePrivateTextUpdate(3, "three")), Is.EqualTo(ChatGateResult.ProcessNow));
    }

    [Test]
    public void TryEnter_Command_BypassesSlot()
    {
        var busy = CreatePrivateTextUpdate(1, "busy");
        _gate.TryEnter(busy);

        var command = CreatePrivateTextUpdate(2, "/start");
        Assert.That(_gate.TryEnter(command), Is.EqualTo(ChatGateResult.Bypass));
    }

    private static UpdateDecorator CreatePrivateTextUpdate(int messageId, string text)
    {
        return new UpdateDecorator(new Update
        {
            Id = messageId,
            Message = new Message
            {
                Id = messageId,
                Date = DateTime.UtcNow,
                Text = text,
                From = new User
                {
                    Id = 42,
                    IsBot = false,
                    FirstName = "Test"
                },
                Chat = new Chat
                {
                    Id = 42,
                    Type = ChatType.Private
                }
            }
        });
    }
}
