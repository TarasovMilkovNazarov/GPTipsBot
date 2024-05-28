namespace GPTipsBot
{
    public static class AppConfig
    {
        public static string BotName { get;set; }
        public static readonly long[] AdminIds = { 486363646, 396539949 }; // Саня, Даня
        public static bool IsOnMaintenance = false;
        public static readonly bool IsDevelopment = Env != "Production";
        public static readonly bool IsProduction = Env == "Production";
        public static string TelegramToken => GetEnvStrict("TELEGRAM_TOKEN");
        public static string ProxyLogin => GetEnvStrict("PROXY_LOGIN");
        public static string ProxyPwd => GetEnvStrict("PROXY_PWD");
        public static string ProxyIP => GetEnvStrict("PROXY_IP");
        public static string ProxyPort => GetEnvStrict("PROXY_PORT");
        public static string ConnectionString => GetEnvStrict("PG_CONNECTION_STRING");
        public static string Env => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        public static string? Version => Environment.GetEnvironmentVariable("GPTIPSBOT_VERSION");
        public static string? CommitHash => Environment.GetEnvironmentVariable("GPTIPSBOT_COMMITHASH");
        public static string? DebugOpenAiApiKey => Environment.GetEnvironmentVariable("DEBUG_OPENAI_TOKEN");
        public static string YandexCloudApiKey => Environment.GetEnvironmentVariable("YC_API_KEY");
        public static string YandexCloudFolderId => Environment.GetEnvironmentVariable("YC_FOLDER_ID");
        public static string TelejetApiKey => Environment.GetEnvironmentVariable("TELEJET_API_KEY");
        public static string ProxyApiApiKey => Environment.GetEnvironmentVariable("WWW_PROXY_API_API_KEY");
        public static string PawanOsmanApiKey => Environment.GetEnvironmentVariable("PAWAN_OSMAN_API_KEY");

        private static string GetEnvStrict(string name)
        {
            var env = Environment.GetEnvironmentVariable(name);
            if (env is null or "")
                throw new ArgumentNullException(name);

            return env;
        }
    }
}