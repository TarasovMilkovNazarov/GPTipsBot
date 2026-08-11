namespace GPTipsBot.Web;

public static class WebAuthConstants
{
    public const string CookieName = "gptips_web_session";
    public const string Scheme = "WebCookie";
    public const string GuestSource = "web-guest";
    public const string TelegramSource = "web-telegram";
    public const string EmailSource = "web-email";
    public const string YandexSource = "web-yandex";
    public const string UserIdClaim = "uid";
    public const string IsGuestClaim = "guest";
    public const string YandexOAuthStateCookie = "gptips_yandex_oauth";

    /// <summary>Positive id range for email/yandex accounts; avoids Telegram id collisions.</summary>
    public const long EmailIdBase = 1_000_000_000_000L;
}
