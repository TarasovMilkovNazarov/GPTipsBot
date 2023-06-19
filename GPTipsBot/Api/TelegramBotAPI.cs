using Microsoft.Extensions.Logging;
using RestSharp;
using Telegram.Bot.Exceptions;

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

        public string? GetErrorMessageFromApiResponse(Exception ex)
        {
            var ErrorMessage = ex switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => ex.ToString()
            };

            return ErrorMessage;
        }
        public void LogErrorMessageFromApiResponse(Exception ex)
        {
            var errorMessage = GetErrorMessageFromApiResponse(ex);
            errorMessage = errorMessage.Length > 100 ? errorMessage.Substring(0, 100) : errorMessage;
            logger.LogError(ex, errorMessage);
        }
    }
}
