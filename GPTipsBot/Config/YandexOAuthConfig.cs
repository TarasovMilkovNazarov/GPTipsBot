namespace GPTipsBot.Config;

/// <summary>
/// Yandex ID OAuth (authorization code). Optional — when unset, UI hides the button.
/// Register app at https://oauth.yandex.ru/client/new/id/ with scopes login:info, login:email.
/// </summary>
public static class YandexOAuthConfig
{
    public static bool IsEnabled =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    public static string? ClientId => Normalize(Environment.GetEnvironmentVariable("YANDEX_OAUTH_CLIENT_ID"));
    public static string? ClientSecret => Normalize(Environment.GetEnvironmentVariable("YANDEX_OAUTH_CLIENT_SECRET"));

    /// <summary>
    /// Optional absolute callback URL. When empty, derived from the current request
    /// as {scheme}://{host}/api/auth/yandex/callback.
    /// </summary>
    public static string? RedirectUri => Normalize(Environment.GetEnvironmentVariable("YANDEX_OAUTH_REDIRECT_URI"));

    public const string AuthorizeUrl = "https://oauth.yandex.ru/authorize";
    public const string TokenUrl = "https://oauth.yandex.ru/token";
    public const string UserInfoUrl = "https://login.yandex.ru/info";
    public const string Scopes = "login:info login:email";

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
