using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot
{
    public static class AppConfig
    {
        public static string TelegramToken { get; }
        public static string OpenAiToken { get; }
        public const long СhatId = 486363646;

        static AppConfig()
        {
            TelegramToken = Environment.GetEnvironmentVariable("TELEGRAM_TOKEN") ?? "";
            OpenAiToken =  Environment.GetEnvironmentVariable("OPENAI_TOKEN") ?? "";
            
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
