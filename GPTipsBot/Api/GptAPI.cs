﻿using GPTipsBot.Dtos;
using GPTipsBot.Extensions;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenAI.GPT3.Interfaces;
using OpenAI.GPT3.ObjectModels.RequestModels;
using OpenAI.GPT3.ObjectModels.ResponseModels;
using RestSharp;
using GptModels = OpenAI.GPT3.ObjectModels;

namespace GPTipsBot.Api
{
    using static AppConfig;

    public class GptAPI
    {
        private readonly string baseUrl1 = "https://chatgpt-api.shn.hk/v1";
        private readonly string baseUrl2 = "https://free.churchless.tech/v1/chat/completions";
        private readonly string baseUrl3 = "https://api.jeeves.ai/generate/v3/chat";
        private readonly ILogger<TelegramBotWorker> logger;
        private readonly IOpenAIService openAiService;
        private readonly MessageService messageService;

        public GptAPI(ILogger<TelegramBotWorker> logger, IOpenAIService openAiService, MessageService messageService)
        {
            this.logger = logger;
            this.openAiService = openAiService;
            this.messageService = messageService;
        }

        public async Task<(bool isSuccessful, string? text)> SendMessage(TelegramGptMessageUpdate telegramGptMessage, CancellationToken token)
        {
            var textWithContext = messageService.PrepareContext(telegramGptMessage.UserKey, telegramGptMessage.ContextId.Value);
            if (textWithContext.Length == 0)
            {
                //todo reset context or suggest user to reset: send inline command with reset
                throw new CustomException(BotResponse.TokensLimitExceeded);
            }

            if (UseFreeApi)
            {
                return SendViaFreeProxy(textWithContext, token);
            }

            return await SendViaOpenAiApi(textWithContext, token);
        }

        public (bool isSuccessful, string? response) SendViaFreeProxy(ChatMessage[] messages, CancellationToken token = default)
        {
            var freeGptClient = new RestClient(baseUrl2);
            var request = new RestRequest("", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            var gptDto = new
            {
                model = GptModels.Models.ChatGpt3_5Turbo,
                messages,
                stream = false
            };

            request.AddBody(gptDto);
            RestResponse? response = null;
            var responseText = "";

            try
            {
                response = freeGptClient.ExecuteWithRetry(request, maxRetries: 10, cancellationToken: token);
                var completionResult = JsonConvert.DeserializeObject<ChatCompletionCreateResponse>(response?.Content);
                string? content = completionResult?.Choices?.FirstOrDefault()?.Message?.Content;

                responseText = content;
            }
            catch (Exception ex)
            {
                logger.LogWithStackTrace(LogLevel.Error, ex.Message);
            }

            return (response?.IsSuccessful ?? false, responseText);
        }

        public async Task<(bool isSuccessful, string? response)> SendViaOpenAiApi(ChatMessage[] messages, CancellationToken token = default)
        {
            await Task.Delay(30000);
            var completionResult = await openAiService.ChatCompletion.CreateCompletion(
                new ChatCompletionCreateRequest
                {
                    Messages = messages,
                    Model = GptModels.Models.ChatGpt3_5Turbo,
                    //MaxTokens = AppConfig.ChatGptTokensLimitPerMessage
                }, cancellationToken: token);

            var response = completionResult.Choices.First()?.Message?.Content;

            return (completionResult?.Successful ?? false, response);
        }
    }
}
