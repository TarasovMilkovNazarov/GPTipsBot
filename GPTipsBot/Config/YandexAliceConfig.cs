namespace GPTipsBot.Config;

public static class YandexAliceConfig
{
    public static string YandexSessionId => GetEnvStrict("YANDEX_SESSION_ID");
    public static string YandexSessionId2 => GetEnvStrict("YANDEX_SESSION_ID2");
    public static string YandexSessar => GetEnvStrict("YANDEX_SESSAR");

    public static string YandexUid => GetEnvStrict("YANDEX_UID");
    public static string YandexLogin => GetEnvStrict("YANDEX_LOGIN");
    public static string YandexYuidss => GetEnvStrict("YANDEX_YUIDSS");

    public static string YandexSae => GetEnvStrict("YANDEX_SAE");
    public static string YandexMy => GetEnvStrict("YANDEX_MY");
    public static string YandexUuid => GetEnvStrict("YANDEX_UUID");

    public static string? YandexYmex => Environment.GetEnvironmentVariable("YANDEX_YMEX");
    public static string? YandexGdpr => Environment.GetEnvironmentVariable("YANDEX_GDPR");
    public static string? YandexGdprB => Environment.GetEnvironmentVariable("YANDEX_GDPR_B");

    // Метод для получения всех кук в формате для запроса
    public static string[] GetAllCookies()
    {
        var cookies = new List<string>
        {
            $"Session_id={YandexSessionId}",
            $"sessionid2={YandexSessionId2}",
            $"sessar={YandexSessar}",
            $"yandexuid={YandexUid}",
            $"yandex_login={YandexLogin}",
            $"yuidss={YandexYuidss}",
            $"sae={YandexSae}",
            $"my={YandexMy}",
            $"uuid={YandexUuid}"
        };

        if (!string.IsNullOrEmpty(YandexYmex))
            cookies.Add($"ymex={YandexYmex}");

        if (!string.IsNullOrEmpty(YandexGdpr))
            cookies.Add($"gdpr={YandexGdpr}");

        if (!string.IsNullOrEmpty(YandexGdprB))
            cookies.Add($"is_gdpr_b={YandexGdprB}");

        return cookies.ToArray();
    }

    // Метод для получения строки Cookie для заголовка
    public static string GetCookieHeader()
    {
        return string.Join("; ", GetAllCookies());
    }

    private static string GetEnvStrict(string name)
    {
        var env = Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrEmpty(env))
            throw new InvalidOperationException($"Environment variable {name} is not set");

        return env;
    }
}