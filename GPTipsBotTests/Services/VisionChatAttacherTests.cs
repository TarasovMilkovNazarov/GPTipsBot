using GPTipsBot.Services;
using NUnit.Framework;
using OpenAI.ObjectModels.RequestModels;

namespace GPTipsBotTests.Services;

[TestFixture]
public class VisionChatAttacherTests
{
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0];
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Test]
    public void AttachToLastUserMessage_AddsSystemDateAndImageContent()
    {
        var messages = new List<ChatMessage>
        {
            ChatMessage.FromUser("Работает ли Юля завтра"),
        };
        var utcNow = new DateTime(2026, 9, 2, 12, 0, 0, DateTimeKind.Utc);

        VisionChatAttacher.AttachToLastUserMessage(messages, JpegBytes, utcNow);

        Assert.That(messages, Has.Count.EqualTo(2));
        Assert.That(messages[0].Role, Is.EqualTo("system"));
        Assert.That(messages[0].Content, Does.Contain("2026-09-02"));
        Assert.That(messages[0].Content, Does.Contain("Wednesday"));
        Assert.That(messages[1].Contents, Is.Not.Null);
        Assert.That(messages[1].Contents![0].Text, Is.EqualTo("Работает ли Юля завтра"));
        Assert.That(messages[1].Contents![1].Type, Is.EqualTo("image_url"));
        Assert.That(messages[1].Contents![1].ImageUrl!.Url, Does.StartWith("data:image/jpeg;base64,"));
        Assert.That(messages[1].Contents![1].ImageUrl!.Detail, Is.EqualTo("high"));
    }

    [Test]
    public void AttachToLastUserMessage_EmptyUserText_UsesDefaultPrompt()
    {
        var messages = new List<ChatMessage> { ChatMessage.FromUser("") };

        VisionChatAttacher.AttachToLastUserMessage(messages, JpegBytes, DateTime.UtcNow);

        Assert.That(messages[^1].Contents![0].Text, Is.EqualTo(VisionChatAttacher.DefaultUserPrompt));
    }

    [Test]
    public void AttachToLastUserMessage_NoUserMessage_DoesNothing()
    {
        var messages = new List<ChatMessage> { ChatMessage.FromAssistant("hi") };

        VisionChatAttacher.AttachToLastUserMessage(messages, JpegBytes, DateTime.UtcNow);

        Assert.That(messages, Has.Count.EqualTo(1));
        Assert.That(messages[0].Content, Is.EqualTo("hi"));
    }

    [Test]
    public void DetectImageSubtype_PngMagicBytes()
    {
        Assert.That(VisionChatAttacher.DetectImageSubtype(PngBytes), Is.EqualTo("png"));
        Assert.That(VisionChatAttacher.DetectImageSubtype(JpegBytes), Is.EqualTo("jpeg"));
    }
}
