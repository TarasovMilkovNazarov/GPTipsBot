namespace GPTipsBot
{
    public static class AppConfig
    {
        public static bool UseFreeApi = false;
        public static bool IsOnMaintenance = false;
        public static string Env => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        public static string TelegramToken => GetEnvStrict("TELEGRAM_TOKEN");
        public static string OpenAiToken => GetEnvStrict("OPENAI_TOKEN");
        public static string ConnectionString => GetEnvStrict("PG_CONNECTION_STRING");
        public const long AdminId = 486363646;

        private static string GetEnvStrict(string name)
        {
            var env = Environment.GetEnvironmentVariable(name);
            if (env is null or "")
                throw new ArgumentNullException(name);

            return env;
        }
    }
}
