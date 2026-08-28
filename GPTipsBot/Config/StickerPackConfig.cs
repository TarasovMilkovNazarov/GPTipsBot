using System.Text.RegularExpressions;

namespace GPTipsBot.Config;

public static class StickerPackConfig
{
    public const int TargetSide = 512;
    public const int EmotionCount = 8;
    public const int MinStickersToPublish = 4;
    public const int VariantParallelism = 3;
    public const string TransparentBackground = "transparent";
    public const string OutputPng = "png";

    public const string ExampleSetName = "stkr486363646_4cb67c_by_GPTipsBot";
    public const string ExamplePackUrl = "https://t.me/addstickers/" + ExampleSetName;

    public static double HeroStars => PaymentConfig.GptImageMedium;
    public static double VariantStars => PaymentConfig.GptImageLow;
    public static double PackRemainderStars => VariantStars * (EmotionCount - 1);

    public const string StylePrefix =
        "Telegram sticker, cute chibi cartoon character, thick white outline, soft drop shadow, " +
        "centered on canvas, bust or full body, simple shapes, no text, no watermark, no extra objects, " +
        "no speech bubble, transparent background";

    public static readonly StickerEmotion[] Emotions =
    [
        new("😊", "happy", "friendly smile, waving hello, looking at the camera", IsHero: true),
        new("😂", "laugh", "laughing with tears, joyful, open mouth"),
        new("😢", "sad", "crying, sad, teary eyes"),
        new("😡", "angry", "angry, frowning, clenched fists"),
        new("😍", "love", "in love, hearts in eyes, blushing"),
        new("👍", "ok", "thumbs up, approving, confident"),
        new("🙏", "please", "folded hands, please or thank you"),
        new("😴", "sleep", "sleeping, peaceful, zzz"),
    ];

    public static string HeroFromTextPrompt(string description) =>
        $"Create a Telegram sticker character: {description}. {StylePrefix}. " +
        $"{Emotions[0].PosePrompt}. Single character only.";

    public static string HeroFromPhotoPrompt(string? extraDescription)
    {
        var extra = string.IsNullOrWhiteSpace(extraDescription)
            ? string.Empty
            : $" Extra notes from the user: {extraDescription.Trim()}.";
        return
            "Turn the person or character in this photo into a Telegram sticker. " +
            $"{StylePrefix}. Keep the same face, hair, clothing, colors and distinctive features. " +
            $"{Emotions[0].PosePrompt}.{extra} Single character only.";
    }

    public static string VariantPrompt(StickerEmotion emotion) =>
        "Same character as the reference image, identical face, hair, clothes, colors and design. " +
        $"{StylePrefix}. Pose and expression: {emotion.PosePrompt}. Do not change the character.";

    public static string BuildSetName(long telegramUserId, string? botUsername)
    {
        var bot = SanitizeToken(botUsername);
        if (string.IsNullOrEmpty(bot))
        {
            bot = "GPTipsBot";
        }

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var prefix = $"stkr{telegramUserId}_{suffix}";
        var name = $"{prefix}_by_{bot}";
        if (name.Length <= 64)
        {
            return name;
        }

        var maxPrefix = 64 - ("_by_".Length + bot.Length);
        if (maxPrefix < 5)
        {
            bot = bot[..Math.Max(1, 64 - 12)];
            maxPrefix = 64 - ("_by_".Length + bot.Length);
        }

        prefix = prefix[..Math.Min(prefix.Length, maxPrefix)].TrimEnd('_');
        return $"{prefix}_by_{bot}";
    }

    /// <summary>
    /// Sticker set title shown in Telegram. @bot is tappable in pack info — the viral attribution path.
    /// </summary>
    public static string BuildSetTitle(string? title, string? botUsername)
    {
        var bot = string.IsNullOrWhiteSpace(botUsername) ? "GPTipsBot" : botUsername.Trim().TrimStart('@');
        if (string.IsNullOrEmpty(bot))
        {
            bot = "GPTipsBot";
        }

        var mention = "@" + bot;
        var baseTitle = string.IsNullOrWhiteSpace(title) ? "GPTips" : title.Trim();
        if (baseTitle.Contains(mention, StringComparison.OrdinalIgnoreCase))
        {
            return baseTitle.Length <= 64 ? baseTitle : baseTitle[..64].TrimEnd();
        }

        var suffix = " · " + mention;
        var maxBase = 64 - suffix.Length;
        if (maxBase < 1)
        {
            return mention.Length <= 64 ? mention : mention[..64];
        }

        if (baseTitle.Length > maxBase)
        {
            baseTitle = baseTitle[..maxBase].TrimEnd();
        }

        return baseTitle + suffix;
    }

    private static readonly Regex NonNameChars = new("[^a-zA-Z0-9_]", RegexOptions.Compiled);

    private static string SanitizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim().TrimStart('@');
        var cleaned = NonNameChars.Replace(trimmed, string.Empty);
        while (cleaned.Contains("__", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("__", "_", StringComparison.Ordinal);
        }

        return cleaned.Trim('_');
    }
}

public sealed record StickerEmotion(string Emoji, string Keyword, string PosePrompt, bool IsHero = false);

public sealed class StickerDraft
{
    public required string Emoji { get; init; }
    public required string Keyword { get; init; }
    public required byte[] Png { get; init; }
}
