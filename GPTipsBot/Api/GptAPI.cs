using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using Timer = System.Timers.Timer;
using GptModels = OpenAI.ObjectModels;
using GPTipsBot.Repositories;
using GPTipsBot.Models;
using GPTipsBot.Services.ChatGpt;

namespace GPTipsBot.Api
{
    public class GptApi : IGpt
    {
        private readonly ILogger<GptApi> logger;
        private readonly OpenaiAccountsRepository openaiAccountsRepository;
        private readonly TokenQueue tokenQueue;
        private readonly MessageService messageService;
        private Timer timer;

        public GptApi(ILogger<GptApi> logger, OpenaiAccountsRepository openaiAccountsRepository,
            MessageService messageService, TokenQueue tokenQueue)
        {
            this.logger = logger;
            this.openaiAccountsRepository = openaiAccountsRepository;
            this.messageService = messageService;
            this.tokenQueue = tokenQueue;
            this.timer = setup_Timer(openaiAccountsRepository);
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

            return await SendViaOpenAiApi(textWithContext, token);
        }

        // TODO: Implement this method with retry policy
        public async Task<ChatCompletionCreateResponse> SendViaOpenAiApi(ChatMessage[] messages, CancellationToken cancellationToken = default)
        {
            var currentToken = await tokenQueue.GetTokenAsync();

            var openAiService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = currentToken
            });

            logger.LogInformation("Send request to OpenAi service: {prompt}", messages.Last().Content);

            ChatCompletionCreateResponse response;
            try
            {
                response = await openAiService.ChatCompletion.CreateCompletion(
                    new ChatCompletionCreateRequest { Messages = messages }, GptModels.Models.ChatGpt3_5Turbo, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                tokenQueue.AddToken(currentToken);
                throw;
            }
            
            if (response.Successful)
            {
                tokenQueue.AddToken(currentToken);
                return response;
            }

            logger.LogError("Failed request to OpenAi service: [{Code}] {Message}, token: {Token}", response.Error?.Code, response.Error?.Message, currentToken[..10]);

            if (response.Error?.Message != null && response.Error.Message.Contains("deactivated"))
            {
                openaiAccountsRepository.Remove(currentToken, DeletionReason.Deactivated);
                await SendViaOpenAiApi(messages, cancellationToken);
            }
            else if (response.Error?.Code == "insufficient_quota")
            {
                openaiAccountsRepository.Remove(currentToken, DeletionReason.InsufficientQuota);
                await SendViaOpenAiApi(messages, cancellationToken);
            }
            else if (response.Error?.Code == "rate_limit_exceeded" && response.Error.Message != null)
            {
                if (response.Error.Message.Contains("on requests per day"))
                {
                    openaiAccountsRepository.FreezeToken(currentToken);
                }
                else if (response.Error.Message.Contains("on requests per min"))
                {
                    tokenQueue.AddToken(currentToken);
                }
                await SendViaOpenAiApi(messages, cancellationToken);
            }
            else
            {
                tokenQueue.AddToken(currentToken);
            }

            return response;
        }

        private Timer setup_Timer(OpenaiAccountsRepository openaiAccountsRepository)
        {
            var delay = TimeSpan.FromMinutes(1).TotalMinutes;

            DateTime nowTime = DateTime.Now;
            DateTime specificTime = nowTime.Date.AddDays(1).AddMinutes(delay);
            if (nowTime > specificTime)
                specificTime = specificTime.AddDays(1);

            double tickTime = (specificTime - nowTime).TotalMilliseconds;
            timer = new Timer(tickTime);
            timer.Elapsed += (s, e) => UnfreezeDayLimitedTokens(openaiAccountsRepository);
            timer.Start();

            return timer;
        }

        private void UnfreezeDayLimitedTokens(OpenaiAccountsRepository openaiAccountsRepository)
        {
            timer.Stop();

            var unfreezed = openaiAccountsRepository.UnfreezeTokens();
            foreach (var item in unfreezed)
            {
                tokenQueue.AddToken(item);
            }

            timer = setup_Timer(openaiAccountsRepository);
        }
    }

    public interface IGpt
    {
        Task<ChatCompletionCreateResponse> SendMessage(UpdateDecorator update, CancellationToken token);
    }
}
