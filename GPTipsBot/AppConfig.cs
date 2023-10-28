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
        public static string ConnectionString => GetEnvStrict("PG_CONNECTION_STRING");
        public static string Env => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        public static string? Version => Environment.GetEnvironmentVariable("GPTIPSBOT_VERSION");
        public static string? CommitHash => Environment.GetEnvironmentVariable("GPTIPSBOT_COMMITHASH");
        public static string? DebugOpenAiToken => Environment.GetEnvironmentVariable("DEBUG_OPENAI_TOKEN");

        private static string GetEnvStrict(string name)
        {
            var env = Environment.GetEnvironmentVariable(name);
            if (env is null or "")
                throw new ArgumentNullException(name);

            return env;
        }
    }
}