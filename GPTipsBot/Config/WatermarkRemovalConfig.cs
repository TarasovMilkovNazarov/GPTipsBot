namespace GPTipsBot.Config;

/// <summary>
/// Watermark removal via VseGPT img2img (ByteDance Seedream 4.5).
/// Seedream is the only edit model that keeps the source aspect ratio instead of
/// forcing a square crop, which is why it is used here instead of GPT Image / Flux.
/// </summary>
public static class WatermarkRemovalConfig
{
    public const string BaseUrl = "https://api.vsegpt.ru/v1/";
    public const string ModelId = "img2img-bytedance/seedream-v4.5-edit-multi";

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

    public static bool IsEnabled => !string.IsNullOrWhiteSpace(AppConfig.VseGptToken);
}
