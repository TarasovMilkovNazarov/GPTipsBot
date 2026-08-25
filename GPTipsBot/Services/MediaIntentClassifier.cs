using System.Text.Json;
using GPTipsBot.Config;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Services;

/// <summary>
/// Last-resort intent detector for messages the regex-based <see cref="NaturalLanguageToolRouter"/>
/// could not classify. Runs a single cheap LLM call (gpt-4o-mini) that must answer with strict JSON;
/// any failure/timeout/parse error fails open to regular chat so it never blocks a reply.
/// </summary>
public class MediaIntentClassifier(IGpt gptService, ILogger<MediaIntentClassifier> logger)
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(6);

    private const string SystemPrompt = """
        You are the intent router for a Telegram bot that can chat, generate images, run OCR on photos,
        build image-generation prompts from photos, remove watermarks, combine two photos, change
        a photo with Yandex Alice, and create Telegram sticker packs. Classify the user's message and reply with STRICT JSON only
        (no markdown, no code fences, no extra text):
        {"intent": "<generate_image|recognize_text|prompt_from_image|remove_watermark|combine_photo|change_photo|sticker_pack|images_menu|none>", "prompt": "<string>"}

        Rules:
        - generate_image: the user wants a picture/drawing/illustration created (e.g. "нарисуй жирафа",
          "draw a cat", "хочу картинку кота"). "prompt" is the subject only, in the user's own language,
          with the request wording ("нарисуй", "draw", etc.) stripped out.
        - recognize_text: the user wants text extracted (OCR) from a photo.
        - prompt_from_image: the user wants a text-to-image prompt built from an existing photo
          (e.g. "промпт по фото", "create a prompt from this image"). NOT ordinary questions about
          what a photo shows — those are "none".
        - remove_watermark: the user wants a watermark, logo, caption or stamp removed/cleaned/erased
          from a photo (e.g. "убери водяной знак с фото", "remove the watermark", "quita la marca de agua").
        - combine_photo: the user wants to merge/combine two photos into one (e.g. "объедини фото",
          "combine these photos").
        - change_photo: the user wants Alice to restyle/change one existing photo (e.g. "измени фото",
          "change this photo", "преобрази в аниме"). Not GPT Image 2 "edit_image".
        - sticker_pack: the user wants a Telegram sticker pack from a photo or a character description
          (e.g. "сделай стикеры", "sticker pack", "создай стикерпак кота"). "prompt" is the character
          description only, or "" if they only asked to start the flow / use the attached photo.
        - images_menu: the user asks in general whether/how the bot can work with images or photos,
          without a concrete request yet.
        - none: anything else — regular chat, questions about a photo they sent (schedules, documents,
          "does X work tomorrow"), or requests unrelated to image tools. Use "none" whenever you are
          not confident.
        - "prompt" must be "" unless intent is "generate_image" or "sticker_pack".
        Reply with the JSON object only.
        """;

    public async Task<MediaToolRoute> TryClassifyAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(RequestTimeout);

            var response = await gptService.SendOneOffAsync(
                SystemPrompt,
                text,
                cts.Token,
                GptModelCatalog.DefaultModelId);

            return Parse(response.Choices.FirstOrDefault()?.Message.Content);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Media intent LLM classification failed; falling back to chat");
            return MediaToolRoute.None;
        }
    }

    private static MediaToolRoute Parse(string? content)
    {
        var json = ExtractJsonObject(content);
        if (json is null)
        {
            return MediaToolRoute.None;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var intentRaw = root.TryGetProperty("intent", out var intentProp) ? intentProp.GetString() : null;
            var intent = intentRaw?.Trim().ToLowerInvariant() switch
            {
                "generate_image" => MediaToolIntent.GenerateImage,
                "recognize_text" => MediaToolIntent.RecognizeText,
                "prompt_from_image" => MediaToolIntent.PromptFromImage,
                "remove_watermark" => MediaToolIntent.RemoveWatermark,
                "combine_photo" => MediaToolIntent.CombinePhoto,
                "change_photo" => MediaToolIntent.ChangePhoto,
                "sticker_pack" => MediaToolIntent.StickerPack,
                "images_menu" => MediaToolIntent.ImagesMenu,
                _ => MediaToolIntent.None,
            };

            if (intent == MediaToolIntent.None)
            {
                return MediaToolRoute.None;
            }

            string? prompt = null;
            if (intent is MediaToolIntent.GenerateImage or MediaToolIntent.StickerPack &&
                root.TryGetProperty("prompt", out var promptProp) &&
                promptProp.GetString() is { Length: > 0 } promptValue)
            {
                prompt = promptValue.Trim();
            }

            return new MediaToolRoute(intent, prompt);
        }
        catch (JsonException)
        {
            return MediaToolRoute.None;
        }
    }

    private static string? ExtractJsonObject(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            trimmed = firstNewline >= 0 ? trimmed[(firstNewline + 1)..] : trimmed;

            var fenceEnd = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (fenceEnd >= 0)
            {
                trimmed = trimmed[..fenceEnd];
            }
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        return start >= 0 && end > start ? trimmed[start..(end + 1)] : null;
    }
}
