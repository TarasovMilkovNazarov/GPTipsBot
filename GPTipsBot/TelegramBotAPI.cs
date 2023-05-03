using GPTipsBot.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using System;
using Telegram.Bot.Exceptions;

namespace GPTipsBot
{
    public class TelegramBotAPI
    {
        private readonly ILogger<TelegramBotAPI> logger;
        private string _botToken;
        private readonly long _chatId;
        private readonly string baseUrl;
        private readonly RestClient telegramHttpClient;
        private string? botDescription;

        public TelegramBotAPI(ILogger<TelegramBotAPI> logger, string botToken, long chatId)
        {
            this.logger = logger;
            _botToken = botToken;
            _chatId = chatId;
            baseUrl = $"https://api.telegram.org/bot{_botToken}";
            telegramHttpClient = new RestClient(baseUrl);
        }
        
        public void SetMyDescription(string messageText)
        {
            var request = new RestRequest("setMyDescription", Method.Post);
            var botDescription = "Description";
            request.AddParameter("chat_id", _chatId);
            request.AddParameter("text", botDescription);
            RestResponse response = null;

            try
            {
                response = telegramHttpClient.Execute(request);
            }
            catch (Exception ex)
            {
                // Handle exception
            }

            if (response != null && response.IsSuccessful)
            {
                Console.WriteLine("Description was set successfully!");
            }
            else
            {
                Console.WriteLine("Error sending message: " + response.StatusDescription);
            }
        }
        public string GetMyDescription()
        {
            if (!string.IsNullOrEmpty(botDescription))
            {
                return botDescription;
            }

            var request = new RestRequest("getMyDescription", Method.Get);
            request.AddParameter("chat_id", _chatId);
            RestResponse? response = null;

            try
            {
                response = telegramHttpClient.Execute(request);
            }
            catch (Exception ex)
            {
                return "";
            }

            dynamic jsonObject = JsonConvert.DeserializeObject(response.Content);
            botDescription = jsonObject.result.description;

            return botDescription;
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
        public string? LogErrorMessageFromApiResponse(Exception ex)
        {
            var ErrorMessage = GetErrorMessageFromApiResponse(ex);
            logger.LogWithStackTrace(LogLevel.Error,ErrorMessage);

            return ErrorMessage;
        }
    }
}
