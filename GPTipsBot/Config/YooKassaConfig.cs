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

    /// <summary>
    /// Gems credited per 1 ₽ paid on the fiat rail. A gem is therefore worth five kopecks at the
    /// default rate: coarse enough that a balance reads as a four-digit number, fine enough that the
    /// cheapest paid action (a chat turn, 4 gems) can still get four times cheaper before the scale
    /// bottoms out and a redenomination is needed.
    /// </summary>
    /// <remarks>
    /// Replaces <c>YOOKASSA_RUB_PER_STAR</c>, which priced the old coarse star unit at 10 ₽. Neither
    /// that name nor a rubles-per-gem spelling is read as a fallback: a leftover value would be off by
    /// orders of magnitude and silently mischarge.
    /// </remarks>
    public static int GemsPerRub
    {
        get
        {
            var raw = Normalize(Environment.GetEnvironmentVariable("YOOKASSA_GEMS_PER_RUB"));
            return int.TryParse(raw, out var value) && value > 0 ? value : 20;
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
