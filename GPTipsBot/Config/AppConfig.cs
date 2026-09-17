using Ardalis.GuardClauses;

namespace GPTipsBot.Config
{
    public static class AppConfig
    {
        public static string BotName { get;set; }
        public static readonly long[] AdminIds = [486363646, 396539949]; // Саня, Даня
        public static bool IsOnMaintenance = false;
        public static readonly bool IsDevelopment = Env != "Production";
        public static readonly bool IsProduction = Env == "Production";
        public static string TelegramToken => GetEnvStrict("TELEGRAM_TOKEN");
        public static string ConnectionString => GetEnvStrict("PG_CONNECTION_STRING");
        public static string Env => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        public static string? Version => Environment.GetEnvironmentVariable("GPTIPSBOT_VERSION");
        public static string? CommitHash => Environment.GetEnvironmentVariable("GPTIPSBOT_COMMITHASH");
        public static string OpenAiToken =>
            IsDevelopment && DebugOpenAiApiKey is not null
                ? DebugOpenAiApiKey
                : GetEnvStrict("OPENAI_TOKEN");
        public static string? DebugOpenAiApiKey => Environment.GetEnvironmentVariable("DEBUG_OPENAI_TOKEN");
        public static string? OpenRouterApiKey => Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        public static string YandexCloudApiKey => GetEnvStrict("YC_API_KEY");
        public static string YandexIamToken => Environment.GetEnvironmentVariable("YC_IAM_TOKEN");
        public static string YandexCloudFolderId => GetEnvStrict("YC_FOLDER_ID");
        public static string? TelejetApiKey => Environment.GetEnvironmentVariable("TELEJET_API_KEY");
        public static string? GramadsBearerToken => Environment.GetEnvironmentVariable("GRAMADS_BEARER");
        public static string HappProxyIp => Environment.GetEnvironmentVariable("HAPP_PROXY_IP") ?? "127.0.0.1";
        public static int HappProxyPort => int.TryParse(Environment.GetEnvironmentVariable("HAPP_PROXY_PORT"), out var port) ? port : 10809;
        public static string? HappProxyLogin => Environment.GetEnvironmentVariable("HAPP_PROXY_LOGIN");
        public static string? HappProxyPassword => Environment.GetEnvironmentVariable("HAPP_PROXY_PASSWORD");

        /// <summary>
        /// t.me deep link back to this bot — the return destination for a payment actually started from
        /// Telegram. Deliberately independent of YOOKASSA_RETURN_URL/LAVATOP_RETURN_URL: those are meant
        /// for the web cabinet flow (see WebApiEndpoints.BuildCabinetReturnUrl) and, when set to the
        /// cabinet's own URL, would otherwise bounce a bot-initiated payment out to the website instead
        /// of back into the chat it started from.
        /// </summary>
        public static string BotDeepLink
        {
            get
            {
                var username =
                    Environment.GetEnvironmentVariable("TELEGRAM_BOT_USERNAME")?.Trim().TrimStart('@')
                    ?? BotName?.TrimStart('@');
                return string.IsNullOrWhiteSpace(username) ? "https://t.me/" : $"https://t.me/{username}";
            }
        }

        private static string GetEnvStrict(string name)
        {
            var env = Environment.GetEnvironmentVariable(name);

            Guard.Against.NullOrEmpty(env, nameof(env));

            return env;
        }
    }
}