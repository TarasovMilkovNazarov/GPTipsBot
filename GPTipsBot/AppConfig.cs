namespace GPTipsBot
{
    public static class AppConfig
    {
        public static readonly long[] AdminIds = { 486363646 };
        public static bool IsOnMaintenance = false;
        public static readonly bool IsDevelopment = Env != "Production";
        public static readonly bool IsProduction = Env == "Production";
        public static string Env => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        public static string TelegramToken => GetEnvStrict("TELEGRAM_TOKEN");
        public static string? DebugOpenAiToken => Environment.GetEnvironmentVariable("DEBUG_OPENAI_TOKEN");
        public static string ConnectionString => GetEnvStrict("PG_CONNECTION_STRING");

        private static string GetEnvStrict(string name)
        {
            var env = Environment.GetEnvironmentVariable(name);
            if (env is null or "")
                throw new ArgumentNullException(name);

            return env;
        }
    }
}