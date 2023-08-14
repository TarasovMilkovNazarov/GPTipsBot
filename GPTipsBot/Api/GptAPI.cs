using GPTipsBot.Extensions;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenAI.Interfaces;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using RestSharp;
using GptModels = OpenAI.ObjectModels;

namespace GPTipsBot.Api
{
    using static AppConfig;

    public class GptAPI
    {
        private readonly string baseUrl1 = "https://chatgpt-api.shn.hk/v1";
        private readonly string baseUrl2 = "https://free.churchless.tech/v1/chat/completions";
        private readonly string baseUrl3 = "https://api.jeeves.ai/generate/v3/chat";
        private readonly string baseUrl4 = "https://api.aiguoguo199.com/v1/chat/completions";
        private readonly ILogger<TelegramBotWorker> logger;
        private readonly IOpenAIService openAiService;
        private readonly MessageService messageService;

        public GptAPI(ILogger<TelegramBotWorker> logger, IOpenAIService openAiService, MessageService messageService)
        {
            this.logger = logger;
            this.openAiService = openAiService;
            this.messageService = messageService;
        }

        public async Task<ChatCompletionCreateResponse> SendMessage(UpdateDecorator update, CancellationToken token)
        {
            ChatMessage[] textWithContext = Array.Empty<ChatMessage>();

            if (update.Message.NewContext)
            {
                textWithContext = new ChatMessage[] { new ChatMessage(update.Message.Role.ToString().ToLower(), update.Message.Text) };
            }
            else
            {
                textWithContext = messageService.PrepareContext(update.UserChatKey, update.Message.ContextId.Value);
            }

            if (textWithContext.Length == 0)
            {
                //todo reset context or suggest user to reset: send inline command with reset
                throw new CustomException(BotResponse.TokensLimitExceeded);
            }

            if (UseFreeApi)
            {
                return await SendViaFreeProxy(textWithContext, token);
            }

            return await SendViaOpenAiApi(textWithContext, token);
        }

        public async Task<ChatCompletionCreateResponse?> SendViaFreeProxy(ChatMessage[] messages, CancellationToken token = default)
        {
            var freeGptClient = new RestClient(baseUrl4);
            var request = new RestRequest("", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization", "Bearer sk-XGNemRUzPF7FlcFR54938d41026b4d84BcA6A2845995Fd51");
            var gptDto = new
            {
                model = GptModels.Models.ChatGpt3_5Turbo,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                stream = false
            };

            request.AddBody(gptDto);
            ChatCompletionCreateResponse? completionResult = null;

            try
            {
                var restResponse = await freeGptClient.ExecuteWithRetry(request, maxRetries: 10, cancellationToken: token);
                completionResult = JsonConvert.DeserializeObject<ChatCompletionCreateResponse>(restResponse?.Content);

                return completionResult;
            }
            catch (OperationCanceledException ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in request to proxy");
            }

            return completionResult;
        }

        public async Task<ChatCompletionCreateResponse> SendViaOpenAiApi(ChatMessage[] messages, CancellationToken token = default)
        {
            var completionResult = await openAiService.ChatCompletion.CreateCompletion(
                new ChatCompletionCreateRequest
                {
                    Messages = messages,
                    Model = GptModels.Models.ChatGpt3_5Turbo,
                    //MaxTokens = AppConfig.ChatGptTokensLimitPerMessage
                }, cancellationToken: token);

            return completionResult;
        }
    }
}
