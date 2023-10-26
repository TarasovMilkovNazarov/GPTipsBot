using Microsoft.Extensions.Logging;
using RestSharp;

namespace GPTipsBot.Api
{
    public class TelegramBotAPI
    {
        private readonly ILogger<TelegramBotAPI> logger;
        private string _botToken;
        private readonly string baseUrl;
        private readonly RestClient telegramHttpClient;
        private string? botDescription;

        public TelegramBotAPI(ILogger<TelegramBotAPI> logger, string botToken)
        {
            this.logger = logger;
            _botToken = botToken;
            baseUrl = $"https://api.telegram.org/bot{_botToken}";
            telegramHttpClient = new RestClient(baseUrl);
        }
    }
}
