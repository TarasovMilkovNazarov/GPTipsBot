using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot
{
    public static class AppConfig
    {
        public static string Env { get; private set; }
        public static string TelegramToken { get; private set; }
        public static string OpenAiToken { get; private set; }
        public static string PG_CONNECTION_STRING { get; private set; }

        public const long СhatId = 486363646;

        static AppConfig()
        {
            TelegramToken = "6272630353:AAG6zDC3BTBQ0dt09nHE6_mN4RpDRUEjPDM";
            OpenAiToken = "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";
            PG_CONNECTION_STRING="Host=localhost;Port=5432;Database=gptips;Username=postgres;Password=postgres";

            Env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            //TelegramToken = Environment.GetEnvironmentVariable("TELEGRAM_TOKEN") ?? "";
            //OpenAiToken =  Environment.GetEnvironmentVariable("OPENAI_TOKEN") ?? "";

            //if (TelegramToken == "")
            //{
            //    throw new ArgumentNullException("Telegram is not configured");
            //}
            //if (OpenAiToken == "")
            //{
            //    throw new ArgumentNullException("Chat GPT is not configured");
            //}
        }
    }
}
