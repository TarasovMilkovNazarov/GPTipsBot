namespace GPTipsBot.Config;

public static class PaymentConfig
{
    // Prices are whole gems. A gem is an abstract unit; at the published fiat rate of
    // YooKassaConfig.GemsPerRub (20) a gem is five kopecks, so divide any price here by 20 for rubles.
    // The unit is deliberately finer than the cheapest action: a chat turn costs 4 gems and could get
    // four times cheaper before the scale runs out and a redenomination is needed.

    /// <summary>gpt-4o-mini. ~4 kopecks of tokens per turn (1000-token context cap + the tool schema),
    /// priced at ~5× that. Every model below is a multiple of this one.</summary>
    public const int Gpt = 4;
    /// <summary>gpt-5-mini (~3× gpt-4o-mini API cost).</summary>
    public const int Gpt5Mini = 12;
    /// <summary>gpt-4.1 (~10× gpt-4o-mini).</summary>
    public const int Gpt41 = 40;
    /// <summary>gpt-5 flagship (~12×, output-heavy).</summary>
    public const int Gpt5 = 48;
    /// <summary>o3 reasoning (~20× with reasoning tokens).</summary>
    public const int GptReasoning = 80;
    /// <summary>Day summary is a larger one-off prompt (~3× a normal GPT reply).</summary>
    public const int Summary = 12;
    public const int Image = 100;
    public const int Animation = 100;
    public const int CombinePhoto = Image;
    public const int ChangePhoto = Image;

    /// <summary>GPT Image 2 low quality (~$0.006 / 1024²).</summary>
    public const int GptImageLow = 200;
    /// <summary>GPT Image 2 medium quality (~$0.053 / 1024²).</summary>
    public const int GptImageMedium = 400;
    /// <summary>GPT Image 2 high quality (~$0.211 / 1024²).</summary>
    public const int GptImageHigh = 800;

    /// <summary>Watermark removal via Seedream 4.5 on OpenRouter ($0.04 / image, ~3.6 ₽ with the top-up fee).</summary>
    public const int WatermarkRemoval = 400;

    /// <summary>
    /// Число бесплатных запросов на генерацию изображений, которое добавляется джобой обновления лимитов
    /// </summary>
    public const int FreeImageGenerations = 3;
    public const int FreeCombinePhotos = FreeImageGenerations;
    public const int FreeChangePhotos = FreeImageGenerations;
    public const int FreeTextRecognitions = 10;
    public const int FreeChatGptRequests = 10;
    public const int FreePhotoAnimations = 2;
    /// <summary>
    /// Число бесплатных запросов для новых пользователей
    /// </summary>
    public const int NewbieFreeImageGenerations = 3;
    public const int NewbieFreeCombinePhotos = NewbieFreeImageGenerations;
    public const int NewbieFreeChangePhotos = NewbieFreeImageGenerations;
    public const int NewbieFreeTextRecognitions = 10;
    public const int NewbieFreeChatGptRequests = 10;
    public const int NewbieFreePhotoAnimations = 2;
    /// <summary>One-time free /summary uses (not refreshed by the daily limits job).</summary>
    public const int NewbieFreeSummaries = 3;

    /// <summary>Abandoned Held payment holds older than this are auto-released.</summary>
    public static readonly TimeSpan PaymentHoldTtl = TimeSpan.FromMinutes(60);

    /// <summary>Minimum YooKassa top-up in rubles.</summary>
    public const int MinRechargeRub = 50;

    /// <summary>Gem packages shown on /deposit — 50, 100, 250, 500 and 1000 ₽ worth.</summary>
    public static int[] DepositGemPackages =>
        [.. new[] { 50, 100, 250, 500, 1_000 }.Select(rub => rub * YooKassaConfig.GemsPerRub)];

    /// <summary>Fewest gems worth at least <see cref="MinRechargeRub"/>.</summary>
    public static int MinRechargeGems => MinRechargeRub * YooKassaConfig.GemsPerRub;

    /// <summary>Alias used across the codebase / tests.</summary>
    public static int MinRechargeAmount => MinRechargeGems;
}
