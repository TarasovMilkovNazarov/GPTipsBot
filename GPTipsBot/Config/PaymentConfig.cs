namespace GPTipsBot.Config;

public static class PaymentConfig
{
    public const double Gpt = 0.1;
    public const double Image = 0.5;
    public const double Animation = 0.5;

    /// <summary>
    /// Число бесплатных запросов на генерацию изображений, которое добавляется джобой обновления лимитов
    /// </summary>
    public const int FreeImageGenerations = 10;
    public const int FreeTextRecognitions = 10;
    public const int FreeChatGptRequests = 10;
    public const int FreePhotoAnimations = 2;
    /// <summary>
    /// Число бесплатных запросов для новых пользователей
    /// </summary>
    public const int NewbieFreeImageGenerations = 10;
    public const int NewbieFreeTextRecognitions = 10;
    public const int NewbieFreeChatGptRequests = 10;
    public const int NewbieFreePhotoAnimations = 2;

    /// <summary>Minimum YooKassa top-up in rubles.</summary>
    public const int MinRechargeRub = 50;

    /// <summary>Star packages shown on /deposit (filtered by <see cref="MinRechargeStars"/>).</summary>
    public static readonly int[] DepositStarPackages = [5, 10, 25, 50, 100];

    /// <summary>Minimum stars so that stars × rub-per-star ≥ <see cref="MinRechargeRub"/>.</summary>
    public static int MinRechargeStars =>
        Math.Max(1, (int)Math.Ceiling(MinRechargeRub / YooKassaConfig.RubPerStar));

    /// <summary>Alias used across the codebase / tests.</summary>
    public static int MinRechargeAmount => MinRechargeStars;
}
