namespace GPTipsBot.Config;

/// <summary>
/// Watermark removal via OpenRouter img2img (ByteDance Seedream 4.5).
/// Seedream is the only edit model that keeps the source aspect ratio instead of
/// forcing a square crop, which is why it is used here instead of GPT Image / Flux.
/// </summary>
public static class WatermarkRemovalConfig
{
    public const string BaseUrl = "https://openrouter.ai/api/v1/";
    public const string ModelId = "bytedance-seed/seedream-4.5";
    public const string Referer = "https://gptips.skolkokomu.ru";

    /// <summary>
    /// Seedream sometimes replaces a corner watermark with a decorative inset panel,
    /// so adding new objects is forbidden explicitly.
    /// </summary>
    public const string Prompt =
        "Remove every watermark from this photo: corner logos, captions and repeated " +
        "semi-transparent text across the frame. Reconstruct the original photo underneath. " +
        "Do not add any new objects, panels, frames, insets or borders. " +
        "Keep composition and colors identical.";

    public static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(3);

    public static bool IsEnabled => !string.IsNullOrWhiteSpace(AppConfig.OpenRouterApiKey);
}
