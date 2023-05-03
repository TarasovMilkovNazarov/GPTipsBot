using GPTipsBot.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenAI.GPT3.Interfaces;
using OpenAI.GPT3.ObjectModels.RequestModels;
using RestSharp;
using GptModels = OpenAI.GPT3.ObjectModels;

namespace GPTipsBot
{
    public class GptAPI
    {
        private readonly string baseUrl1 = "https://chatgpt-api.shn.hk/v1";
        private readonly string baseUrl2 = "https://free.churchless.tech/v1/chat/completions";
        private readonly string baseUrl3 = "https://api.jeeves.ai/generate/v3/chat";
        private readonly ILogger<TelegramBotWorker> logger;
        private readonly IOpenAIService openAiService;

        private readonly bool isFreeApi = true;

        public GptAPI(ILogger<TelegramBotWorker> logger, IOpenAIService openAiService)
        {
            this.logger = logger;
            this.openAiService = openAiService;
        }

        public async Task<(bool isSuccessful, string? response)> SendMessage(string messageText)
        {
            if (isFreeApi)
            {
                return SendViaFreeProxy(messageText);
            }

            return await SendViaOpenAiApi(messageText);
        }

        public (bool isSuccessful, string? response) SendViaFreeProxy(string messageText)
        {
            var freeGptClient = new RestClient(baseUrl2);
            var request = new RestRequest("", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddBody(new
            {
                model = "gpt-3.5-turbo",
                messages = new object[] { new { role = "user", content = messageText } },
                stream = false
            });
            RestResponse response = null;
            var responseText = "";

            try
            {
                response = freeGptClient.Execute(request);
                dynamic result = JsonConvert.DeserializeObject(response.Content);
                string content = result.choices[0].message.content;

                responseText = content;
            }
            catch (Exception ex)
            {
                logger.LogWithStackTrace(LogLevel.Error, ex.Message);
            }

            return (response?.IsSuccessful ?? false, responseText);
        }

        public async Task<(bool isSuccessful, string? response)> SendViaOpenAiApi(string message)
        {
            var completionResult = await openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = new List<ChatMessage>
                    {
                        ChatMessage.FromUser(message),
                    },
                Model = GptModels.Models.ChatGpt3_5Turbo,
                MaxTokens = AppConfig.ChatGptTokensLimitPerMessage//optional
            });

            var response = completionResult.Choices.First()?.Message?.Content;

            return (completionResult.Successful, response);
        }
    }
}
