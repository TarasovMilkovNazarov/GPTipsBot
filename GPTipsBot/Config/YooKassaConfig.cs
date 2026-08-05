namespace GPTipsBot.Config;

public static class YooKassaConfig
{
    public static bool IsEnabled =>
        !string.IsNullOrWhiteSpace(ShopId) && !string.IsNullOrWhiteSpace(SecretKey);

    public static string? ShopId => Normalize(Environment.GetEnvironmentVariable("YOOKASSA_SHOP_ID"));
    public static string? SecretKey => Normalize(Environment.GetEnvironmentVariable("YOOKASSA_SECRET_KEY"));

    /// <summary>
    /// API secret keys look like test_... / live_... (Merchant Profile → Integration → API keys).
    /// shopPassword from old HTTP protocol is not accepted.
    /// </summary>
    public static bool HasValidSecretKeyFormat =>
        SecretKey is not null &&
        (SecretKey.StartsWith("test_", StringComparison.Ordinal) ||
         SecretKey.StartsWith("live_", StringComparison.Ordinal));

    /// <summary>Rubles charged per 1 Star credited to the wallet.</summary>
    public static decimal RubPerStar
    {
        get
        {
            var raw = Normalize(Environment.GetEnvironmentVariable("YOOKASSA_RUB_PER_STAR"));
            return decimal.TryParse(raw, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0
                ? value
                : 10m;
        }
    }

    /// <summary>URL where the user returns after paying (typically https://t.me/BotName).</summary>
    public static string ReturnUrl
    {
        get
        {
            var configured = Normalize(Environment.GetEnvironmentVariable("YOOKASSA_RETURN_URL"));
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            return string.IsNullOrWhiteSpace(AppConfig.BotName)
                ? "https://t.me/"
                : $"https://t.me/{AppConfig.BotName.TrimStart('@')}";
        }
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Trim('"', '\'');
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
