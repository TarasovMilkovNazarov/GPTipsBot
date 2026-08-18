using System.Globalization;
using OpenAI.ObjectModels.RequestModels;

namespace GPTipsBot.Services;

/// <summary>
/// Turns the latest user chat turn into a vision (text + image) message and
/// injects the current date so relative questions about calendars/schedules work.
/// </summary>
public static class VisionChatAttacher
{
    public const string DefaultUserPrompt =
        "Look at this image. Answer questions about it. If there is no explicit question, briefly describe what you see.";

    public static string BuildSystemPrompt(DateTime utcNow) =>
        "The current date is " +
        utcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
        " (" + utcNow.ToString("dddd", CultureInfo.InvariantCulture) + ", UTC). " +
        "When the user asks about relative dates (today, tomorrow, yesterday, this week) " +
        "or about calendars, schedules and tables in the image, use this date. " +
        "Read the image carefully, including small text, names, numbers and color coding.";

    public static void AttachToLastUserMessage(
        IList<ChatMessage> messages,
        byte[] imageBytes,
        DateTime utcNow,
        string? fallbackUserText = null)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(imageBytes);

        if (imageBytes.Length == 0)
        {
            return;
        }

        var lastUserIndex = -1;
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (string.Equals(messages[i].Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                lastUserIndex = i;
                break;
            }
        }

        if (lastUserIndex < 0)
        {
            return;
        }

        var text = ResolveUserText(messages[lastUserIndex], fallbackUserText);
        var subtype = DetectImageSubtype(imageBytes);

        messages[lastUserIndex] = ChatMessage.FromUser(
        [
            MessageContent.TextContent(text),
            MessageContent.ImageBinaryContent(imageBytes, subtype, "high"),
        ]);

        if (messages.Count == 0 ||
            !string.Equals(messages[0].Role, "system", StringComparison.OrdinalIgnoreCase) ||
            messages[0].Content?.Contains("current date", StringComparison.OrdinalIgnoreCase) != true)
        {
            messages.Insert(0, ChatMessage.FromSystem(BuildSystemPrompt(utcNow)));
        }
    }

    public static string DetectImageSubtype(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50)
        {
            return "png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0x47 && bytes[1] == 0x49)
        {
            return "gif";
        }

        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return "webp";
        }

        return "jpeg";
    }

    private static string ResolveUserText(ChatMessage message, string? fallbackUserText)
    {
        if (!string.IsNullOrWhiteSpace(message.Content))
        {
            return message.Content;
        }

        var existingText = message.Contents?
            .FirstOrDefault(c => c.Type == "text" && !string.IsNullOrWhiteSpace(c.Text))
            ?.Text;
        if (!string.IsNullOrWhiteSpace(existingText))
        {
            return existingText;
        }

        return string.IsNullOrWhiteSpace(fallbackUserText) ? DefaultUserPrompt : fallbackUserText.Trim();
    }
}
