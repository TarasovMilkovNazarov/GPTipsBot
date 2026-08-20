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
        public static string? HappSubscriptionUrl => Environment.GetEnvironmentVariable("HAPP_SUBSCRIPTION_URL");
        public static string HappProxyIp => Environment.GetEnvironmentVariable("HAPP_PROXY_IP") ?? "127.0.0.1";
        public static int HappProxyPort => int.TryParse(Environment.GetEnvironmentVariable("HAPP_PROXY_PORT"), out var port) ? port : 10809;
        public static string? HappProxyLogin => Environment.GetEnvironmentVariable("HAPP_PROXY_LOGIN");
        public static string? HappProxyPassword => Environment.GetEnvironmentVariable("HAPP_PROXY_PASSWORD");

        private static string GetEnvStrict(string name)
        {
            var env = Environment.GetEnvironmentVariable(name);

            Guard.Against.NullOrEmpty(env, nameof(env));

            return env;
        }
    }
}