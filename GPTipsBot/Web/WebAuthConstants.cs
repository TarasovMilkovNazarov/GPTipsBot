namespace GPTipsBot.Web;

public static class WebAuthConstants
{
    public const string CookieName = "gptips_web_session";
    public const string Scheme = "WebCookie";
    public const string GuestSource = "web-guest";
    public const string TelegramSource = "web-telegram";
    public const string EmailSource = "web-email";
    public const string UserIdClaim = "uid";
    public const string IsGuestClaim = "guest";

    /// <summary>Positive id range for email accounts; avoids Telegram id collisions.</summary>
    public const long EmailIdBase = 1_000_000_000_000L;
}
