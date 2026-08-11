using System.Text.RegularExpressions;
using GPTipsBot.UpdateHandlers;

namespace GPTipsBot.Services;

public enum MediaToolIntent
{
    None = 0,
    /// <summary>Yandex/GPT-IMAGE-1 free generation flow (/image).</summary>
    GenerateImage = 1,
    /// <summary>OCR (/get_image_text).</summary>
    RecognizeText = 2,
    /// <summary>Vision prompt-from-image (/prompt_from_image) — also used for “what’s on the photo”.</summary>
    PromptFromImage = 3,
    /// <summary>Show images menu (capability ask / bare photo / ambiguous media).</summary>
    ImagesMenu = 4,
    /// <summary>Greeting or “what can you do” — show onboarding instead of ChatGPT.</summary>
    Onboarding = 5,
}

public readonly record struct MediaToolRoute(MediaToolIntent Intent, string? Prompt = null)
{
    public static MediaToolRoute None => new(MediaToolIntent.None);
}

/// <summary>
/// Routes free-text (and photo captions) to existing media commands so chat does not
/// answer “I am a text-only model” for image/OCR asks.
/// </summary>
public static class NaturalLanguageToolRouter
{
    private static readonly RegexOptions Rx =
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    private static readonly Regex OcrRegex = new(
        @"(?:^|\b)(?:" +
        @"распознай(?:\s+текст)?(?:\s+(?:с|на)\s+(?:фото|картинк\w*|изображени\w*))?" +
        @"|текст\s+с\s+(?:фото|картинк\w*|изображени\w*)" +
        @"|extract(?:\s+the)?\s+text(?:\s+from(?:\s+(?:the|this))?\s+(?:photo|image|picture))?" +
        @"|(?:read|recognize)\s+(?:the\s+)?text(?:\s+from(?:\s+(?:the|this))?\s+(?:photo|image|picture))?" +
        @"|\bocr\b" +
        @"|extrae(?:r)?\s+(?:el\s+)?texto(?:\s+de(?:\s+(?:la|esta))?\s+(?:foto|imagen))?" +
        @")",
        Rx);

    private static readonly Regex PromptFromImageRegex = new(
        @"(?:^|\b)(?:" +
        @"(?:создай|сгенерируй|сделай)\s+промпт(?:\s+(?:по|из|с|для))?\s*(?:фото|картинк\w*|изображени\w*)?" +
        @"|prompt\s+from\s+(?:(?:the|this)\s+)?(?:photo|image|picture)" +
        @"|(?:create|generate|make)\s+(?:a\s+)?prompt\s+from\s+(?:(?:the|this)\s+)?(?:photo|image|picture)" +
        @"|промпт\s+(?:по|из|с)\s+(?:фото|картинк\w*|изображени\w*)" +
        @")",
        Rx);

    private static readonly Regex DescribeAttachedPhotoRegex = new(
        @"^(?:" +
        @"что\s+(?:это|на\s+(?:этом\s+)?(?:фото|картинк\w*|изображени\w*))\??" +
        @"|опиши(?:\s+(?:это|данное))?\s+(?:фото|картинк\w*|изображени\w*)" +
        @"|what(?:'s|\s+is)\s+(?:in|on|this)(?:\s+(?:the|this))?\s*(?:photo|image|picture)?\??" +
        @"|describe(?:\s+(?:this|the))?\s+(?:photo|image|picture)" +
        @"|look\s+at(?:\s+(?:this|the))?\s+(?:photo|image|picture)" +
        @"|qu[eé]\s+hay\s+en\s+(?:la\s+|esta\s+)?(?:foto|imagen)\??" +
        @"|describe(?:\s+(?:la|esta))?\s+(?:foto|imagen)" +
        @"|зацени(?:шь)?(?:\s+\w+)?\s+по\s+фото" +
        @")$",
        Rx);

    private static readonly Regex GenerateWithPromptRegex = new(
        @"^(?:" +
        @"(?:нарисуй|сгенерируй|создай|сделай)\s+(?:мне\s+)?(?:картинку|изображение|рисунок|фото|иллюстрацию)" +
        @"(?:\s+(?:с|про|на\s+тему|of|about))?\s*[:\-—]?\s*(?<prompt>.+)" +
        @"|(?:create|generate|draw|make|paint)\s+(?:me\s+)?(?:an?\s+)?(?:image|picture|photo|illustration|drawing)" +
        @"(?:\s+of)?\s*[:\-—]?\s*(?<prompt>.+)" +
        @"|(?:crea|genera|haz|dibuja)\s+(?:una?\s+)?(?:imagen|foto|dibujo|ilustraci[oó]n)" +
        @"(?:\s+de)?\s*[:\-—]?\s*(?<prompt>.+)" +
        @")$",
        Rx);

    private static readonly Regex GenerateBareRegex = new(
        @"^(?:" +
        @"(?:нарисуй|сгенерируй|создай|сделай)\s+(?:мне\s+)?(?:картинку|изображение|рисунок|фото|иллюстрацию)" +
        @"|(?:create|generate|draw|make|paint)\s+(?:me\s+)?(?:an?\s+)?(?:image|picture|photo|illustration|drawing)" +
        @"|(?:crea|genera|haz|dibuja)\s+(?:una?\s+)?(?:imagen|foto|dibujo|ilustraci[oó]n)" +
        @"|necesito\s+crear\s+im[aá]genes?(?:\s+en\s+photo)?" +
        @")\s*[.!?]*$",
        Rx);

    private static readonly Regex ImageCapabilityAskRegex = new(
        @"^(?:" +
        @"(?:ты\s+)?(?:можешь|умеешь)\s+(?:ли\s+)?" +
        @"(?:создавать|генерировать|рисовать|делать)\s+(?:картинк\w*|изображен\w*|фото|рисунк\w*)" +
        @"|(?:can|could)\s+you\s+(?:create|generate|draw|make|edit)\s+(?:(?:an?\s+)?(?:images?|pictures?|photos?)|photos?)" +
        @"|(?:puedes|sabes)\s+(?:crear|generar|dibujar|editar)\s+(?:im[aá]genes?|fotos?|dibujos?)" +
        @"|(?:фотки|картинки|изображения)\s+умеешь(?:\s+генерировать)?" +
        @"|да\s+хз,?\s+фотки\s+умеешь\s+генерировать\??" +
        @")\s*[.!?]*$",
        Rx);

    private static readonly Regex GreetingRegex = new(
        @"^(?:" +
        @"hi|hello|hey|hola|yo|sup" +
        @"|привет|здравствуй(?:те)?|здарова|добрый\s+(?:день|вечер|утро)|доброго\s+(?:дня|вечера|утра)" +
        @"|how\s+are\s+you(?:\s+doing)?" +
        @"|как\s+дела|как\s+ты" +
        @"|salam|سلام(?:\s+علیكم)?" +
        @"|buen[oa]s(?:\s+(?:d[ií]as|tardes|noches))?" +
        @")\s*[!?.]*$",
        Rx);

    private static readonly Regex LonePunctuationRegex = new(
        @"^[?？¿!]+$",
        Rx);

    private static readonly Regex OnboardingCapabilityRegex = new(
        @"^(?:" +
        @"что\s+(?:ты\s+)?(?:умеешь|можешь)(?:\s+делать)?" +
        @"|какие\s+(?:у\s+тебя\s+)?(?:есть\s+)?(?:команды|функции|возможности)" +
        @"|как\s+(?:тобой\s+)?пользоваться|как\s+ты\s+работаешь" +
        @"|(?:на)?помни(?:шь)?\s+(?:какие\s+)?(?:твои\s+)?функци\w*" +
        @"|what\s+can\s+you\s+do(?:\s+for\s+me)?" +
        @"|how\s+do\s+(?:you|i)\s+(?:work|use(?:\s+you)?)" +
        @"|what\s+are\s+your\s+(?:commands|features|capabilities)" +
        @"|who\s+are\s+you|what\s+are\s+you" +
        @"|кто\s+ты(?:\s+такой)?" +
        @"|que\s+puedes\s+hacer|qu[eé]\s+sabes\s+hacer" +
        @"|cu[aá]les\s+son\s+tus\s+(?:funciones|comandos)" +
        @"|me\s+puedes\s+recordar\s+(?:cu[aá]les\s+son\s+)?tus\s+funciones" +
        @")\s*[!?.]*$",
        Rx);

    private static readonly Regex MediaHintRegex = new(
        @"(?:фото|картинк|изображен|image|picture|photo|imagen|dibujo|промпт|prompt|нарису|сгенерир)",
        Rx);

    public static MediaToolRoute TryMatch(string? text, bool hasPhoto)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return hasPhoto ? new MediaToolRoute(MediaToolIntent.ImagesMenu) : MediaToolRoute.None;
        }

        if (InlineQueryHandler.TryParse(trimmed, "image", out var inlinePrompt))
        {
            return new MediaToolRoute(MediaToolIntent.GenerateImage, inlinePrompt);
        }

        if (trimmed.Length <= 280 && OcrRegex.IsMatch(trimmed))
        {
            return new MediaToolRoute(MediaToolIntent.RecognizeText);
        }

        if (trimmed.Length <= 280 &&
            (PromptFromImageRegex.IsMatch(trimmed) || DescribeAttachedPhotoRegex.IsMatch(trimmed)))
        {
            return new MediaToolRoute(MediaToolIntent.PromptFromImage);
        }

        if (TryMatchGenerate(trimmed, out var prompt))
        {
            return new MediaToolRoute(MediaToolIntent.GenerateImage, prompt);
        }

        if (trimmed.Length <= 160 && ImageCapabilityAskRegex.IsMatch(trimmed))
        {
            return new MediaToolRoute(MediaToolIntent.ImagesMenu);
        }

        if (hasPhoto && trimmed.Length <= 120 && MediaHintRegex.IsMatch(trimmed))
        {
            return new MediaToolRoute(MediaToolIntent.ImagesMenu);
        }

        if (trimmed.Length <= 120 &&
            (LonePunctuationRegex.IsMatch(trimmed) ||
             GreetingRegex.IsMatch(trimmed) ||
             OnboardingCapabilityRegex.IsMatch(trimmed)))
        {
            return new MediaToolRoute(MediaToolIntent.Onboarding);
        }

        return MediaToolRoute.None;
    }

    private static bool TryMatchGenerate(string text, out string? prompt)
    {
        prompt = null;

        var withPrompt = GenerateWithPromptRegex.Match(text);
        if (withPrompt.Success)
        {
            var extracted = withPrompt.Groups["prompt"].Value.Trim();
            if (extracted.Length > 0)
            {
                prompt = extracted;
                return true;
            }
        }

        return GenerateBareRegex.IsMatch(text);
    }
}
