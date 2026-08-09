namespace GPTipsBot.Web;

public static class WebAuthConstants
{
    public const string CookieName = "gptips_web_session";
    public const string Scheme = "WebCookie";
    public const string GuestSource = "web-guest";
    public const string TelegramSource = "web-telegram";
    public const string UserIdClaim = "uid";
    public const string IsGuestClaim = "guest";
}
