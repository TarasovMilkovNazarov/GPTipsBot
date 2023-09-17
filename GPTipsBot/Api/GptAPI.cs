using GPTipsBot.Extensions;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Interfaces;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using RestSharp;
using Timer = System.Timers.Timer;
using GptModels = OpenAI.ObjectModels;
using System.Timers;
using GPTipsBot.Repositories;
using GPTipsBot.Models;

namespace GPTipsBot.Api
{
    using static AppConfig;

    public class GptAPI
    {
        private readonly string betterChatGptBaseUrl = "https://free.churchless.tech/v1/chat/completions";
        private readonly ILogger<UpdateHandlerEntryPoint> logger;
        private readonly OpenaiAccountsRepository openaiAccountsRepository;
        private IOpenAIService openAiService;
        private readonly MessageService messageService;
        private static Timer timer;
        private static Queue<string> OpenAiTokens;

        public GptAPI(ILogger<UpdateHandlerEntryPoint> logger, 
            OpenaiAccountsRepository openaiAccountsRepository, MessageService messageService)
        {
            this.logger = logger;
            this.openaiAccountsRepository = openaiAccountsRepository;
            this.messageService = messageService;
        }

        static GptAPI()
        {
            var tokensRepository = new OpenaiAccountsRepository(null, new Db.ApplicationContext());
            setup_Timer(tokensRepository);
            OpenAiTokens = new Queue<string>(tokensRepository.GetAllAvailable().Select(x => x.Token));
        }

        public async Task<ChatCompletionCreateResponse> SendMessage(UpdateDecorator update, CancellationToken token)
        {
            ChatMessage[] textWithContext;

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
            var freeGptClient = new RestClient(betterChatGptBaseUrl);
            var request = new RestRequest("", Method.Post);
            request.AddHeader("Content-Type", "application/json");
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
            var currentToken = OpenAiTokens.Dequeue();

            this.openAiService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey =  currentToken
            });

            var completionResult = await openAiService.ChatCompletion.CreateCompletion(
                new ChatCompletionCreateRequest
                {
                    Messages = messages,
                    Model = GptModels.Models.ChatGpt3_5Turbo,
                    //MaxTokens = AppConfig.ChatGptTokensLimitPerMessage
                }, cancellationToken: token);

            if (completionResult.Successful)
            {
                OpenAiTokens.Enqueue(currentToken);
            }
            else if(completionResult.Error?.Message != null && completionResult.Error.Message.Contains("deactivated"))
            {
                openaiAccountsRepository.Remove(currentToken, DeletionReason.Deactivated);
                await SendViaOpenAiApi(messages, token);
            }
            else if(completionResult.Error?.Code == "insufficient_quota")
            {
                openaiAccountsRepository.Remove(currentToken, DeletionReason.InsufficientQuota);
                await SendViaOpenAiApi(messages, token);
            }
            else if(completionResult.Error?.Code == "rate_limit_exceeded" && completionResult.Error.Message != null) {
                if (completionResult.Error.Message.Contains("on requests per day"))
                {
                    openaiAccountsRepository.FreezeToken(currentToken);
                }
                else if (completionResult.Error.Message.Contains("on requests per min"))
                {
                    OpenAiTokens.Enqueue(currentToken);
                }
                await SendViaOpenAiApi(messages, token);
            }

            return completionResult;
        }

        private static void setup_Timer(OpenaiAccountsRepository openaiAccountsRepository)
        {
            var delay = TimeSpan.FromMinutes(1).TotalMinutes;

            DateTime nowTime = DateTime.Now;
            DateTime specificTime = nowTime.Date.AddDays(1).AddMinutes(delay);
            if (nowTime > specificTime)
                specificTime= specificTime.AddDays(1);

            double tickTime = (specificTime- nowTime).TotalMilliseconds;
            timer = new Timer(tickTime);
            timer.Elapsed += (s, e) => UnfreezeDayLimitedTokens(s,e,openaiAccountsRepository);
            timer.Start();
        }

        private static void UnfreezeDayLimitedTokens(object sender, ElapsedEventArgs e, OpenaiAccountsRepository openaiAccountsRepository)
        {
            timer.Stop();

            var unfreezed = openaiAccountsRepository.UnfreezeTokens();
            foreach (var item in unfreezed)
            {
                OpenAiTokens.Enqueue(item);
            }

            setup_Timer(openaiAccountsRepository);
        }
    }
}
