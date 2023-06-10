namespace GPTipsBot
{
    public static class AppConfig
    {
        public static bool UseFreeApi = true;
        public static bool IsOnMaintenance = false;
        public static string Env { get; private set; }
        public static string TelegramToken { get; private set; }
        public static string OpenAiToken { get; private set; }

        public const long СhatId = 486363646;
        public const long AdminId = 486363646;
        public const int ChatGptTokensLimitPerMessage = 100;

        static AppConfig()
        {
            Env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            TelegramToken = Environment.GetEnvironmentVariable("TELEGRAM_TOKEN") ?? "";
            OpenAiToken = Environment.GetEnvironmentVariable("OPENAI_TOKEN") ?? "";
            //Console.WriteLine($"TELEGRAM_TOKEN {TelegramToken}");
            //Console.WriteLine($"OPENAI_TOKEN {OpenAiToken}");

            if (TelegramToken == "")
            {
                throw new ArgumentNullException("Telegram is not configured");
            }
            if (OpenAiToken == "")
            {
                throw new ArgumentNullException("Chat GPT is not configured");
            }
        }
    }
}
