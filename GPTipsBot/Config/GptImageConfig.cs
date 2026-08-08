namespace GPTipsBot.Config;

/// <summary>
/// GPT Image 2 options and Stars pricing (paid only).
/// Prices map to OpenAI output cost tiers for ~1024px images.
/// </summary>
public static class GptImageConfig
{
    public const string ModelId = "gpt-image-2";
    public const int PromptLimit = 4000;

    public const string SizeSquare = "1024x1024";
    public const string SizeLandscape = "1536x1024";
    public const string SizePortrait = "1024x1536";

    public const string QualityLow = "low";
    public const string QualityMedium = "medium";
    public const string QualityHigh = "high";

    public static readonly GptImageSizeOption[] Sizes =
    [
        new(SizeSquare, "1:1", "🟦"),
        new(SizeLandscape, "3:2", "🟦🟦"),
        new(SizePortrait, "2:3", "🟦\n🟦"),
    ];

    public static readonly GptImageQualityOption[] Qualities =
    [
        new(QualityLow, "Low", PaymentConfig.GptImageLow),
        new(QualityMedium, "Medium", PaymentConfig.GptImageMedium),
        new(QualityHigh, "High", PaymentConfig.GptImageHigh),
    ];

    public static GptImageSizeOption ResolveSize(string? size) =>
        Sizes.FirstOrDefault(s => string.Equals(s.Id, size, StringComparison.OrdinalIgnoreCase))
        ?? Sizes[0];

    public static GptImageQualityOption ResolveQuality(string? quality) =>
        Qualities.FirstOrDefault(q => string.Equals(q.Id, quality, StringComparison.OrdinalIgnoreCase))
        ?? Qualities[1];

    public static double PriceFor(string? quality) => ResolveQuality(quality).StarsCost;
}

public sealed record GptImageSizeOption(string Id, string Label, string Emoji);

public sealed record GptImageQualityOption(string Id, string Label, double StarsCost);
