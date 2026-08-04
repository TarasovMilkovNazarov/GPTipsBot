namespace GPTipsBot.Config;

public static class PaymentConfig
{
    public const double Gpt = 0.1;
    public const double Image = 0.5;

    /// <summary>
    /// Число бесплатных запросов на генерацию изображений, которое добавляется джобой обновления лимитов
    /// </summary>
    public const int FreeImageGenerations = 10;
    public const int FreeTextRecognitions = 10;
    public const int FreeChatGptRequests = 10;
    /// <summary>
    /// Число бесплатных запросов для новых пользователей
    /// </summary>
    public const int NewbieFreeImageGenerations = 10;
    public const int NewbieFreeTextRecognitions = 10;
    public const int NewbieFreeChatGptRequests = 10;
    public const int MinRechargeAmount = 1;
};