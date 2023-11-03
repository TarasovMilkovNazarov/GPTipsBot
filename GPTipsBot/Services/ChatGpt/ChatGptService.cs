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
using Polly;
using GPTipsBot.Exceptions;
using TiktokenSharp;
using GPTipsBot.Dtos;
using GPTipsBot.Resources;

namespace GPTipsBot.Services
{
    public class ChatGptService : IGpt
    {
        private readonly ILogger<ChatGptService> log;
        private readonly OpenaiAccountsRepository openaiAccountsRepository;
        private readonly TokenQueue tokenQueue;
        private readonly MessageRepository messageRepository;
        private Timer timer;

        public ChatGptService(ILogger<ChatGptService> log, OpenaiAccountsRepository openaiAccountsRepository,
            MessageRepository messageRepository, TokenQueue tokenQueue)
        {
            this.log = log;
            this.openaiAccountsRepository = openaiAccountsRepository;
            this.messageRepository = messageRepository;
            this.tokenQueue = tokenQueue;
            timer = setup_Timer(openaiAccountsRepository);
        }

        public async Task<ChatCompletionCreateResponse?> SendMessage(UpdateDecorator update, CancellationToken token)
        {
            ChatMessage[] textWithContext;

            if (update.Message.NewContext)
            {
                textWithContext = new ChatMessage[] { new ChatMessage(update.Message.Role.ToString().ToLower(), update.Message.Text) };
            }
            else
            {
                textWithContext = PrepareContext(update.UserChatKey, update.Message.ContextId.Value);
            }

            return await SendMessageInternal(textWithContext, token);
        }

        public ChatMessage[] PrepareContext(UserChatKey userKey, long contextId)
        {
            var messages = messageRepository
                .GetRecentContextMessages(userKey, contextId).Where(x => !string.IsNullOrEmpty(x.Text));
            var contextWindow = new ContextWindow();

            foreach (var item in messages)
            {
                var isMessageAddedToContext = contextWindow.TryToAddMessage(item.Text, item.Role.ToString().ToLower(), out var messageTokensCount);
                if (isMessageAddedToContext)
                {
                    continue;
                }

                if (contextWindow.TokensCount == 0)
                {
                    //todo reset context or suggest user to reset: send inline command with reset
                    throw new ClientException(string.Format(BotResponse.TokensLimitExceeded, ContextWindow.TokensLimit, messageTokensCount));
                }

                break;
            }

            return contextWindow.GetContext();
        }

        private async Task<ChatCompletionCreateResponse?> SendMessageInternal(ChatMessage[] messages, CancellationToken cancellationToken)
        {
            const int maxRetryCount = 4;

            log.LogInformation("Send request to OpenAi service: {messages}", messages.Last().Content);

            ChatCompletionCreateResponse? response = null;

            var policy = Policy
                .Handle<ChatGptException>()
                .WaitAndRetryAsync(maxRetryCount, (retryAttempt) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException();
                    }

                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));

                    return delay;
                });

            await policy.ExecuteAsync(async (context, cancellationToken) =>
            {
                var currentToken = await tokenQueue.GetTokenAsync();
                var openAiService = new OpenAIService(new OpenAiOptions()
                {
                    ApiKey = currentToken
                });

                var retryAttempt = context.ContainsKey("retryAttempt")
                    ? (int)context["retryAttempt"] : 0;

                try
                {
                    response = await openAiService.ChatCompletion.CreateCompletion(
                        new ChatCompletionCreateRequest { Messages = messages }, 
                        GptModels.Models.Gpt_3_5_Turbo, cancellationToken);

                    if (response.Successful)
                    {
                       tokenQueue.AddToken(currentToken);
                       return;
                    }
                }
                catch (OperationCanceledException)
                {
                    tokenQueue.AddToken(currentToken);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                }
                catch (Exception ex) {
                    //exceptions handled below
                }

                context["retryAttempt"] = retryAttempt + 1;

                HandleResponseErrors(response, currentToken);
                log.LogInformation("Failed request #{retryAttempt} to OpenAi service: [{Code}] {Message}", 
                    retryAttempt, response?.Error?.Code, response?.Error?.Message);
                throw new ChatGptException(retryAttempt);
            }, new Context(), cancellationToken);

            return response;
        }

        
        public static long CountTokens(string message)
        {
            TikToken tikToken = TikToken.EncodingForModel("gpt-3.5-turbo");
            var i = tikToken.Encode(message); //[15339, 1917]

            return i.Count;
        }

        private void HandleResponseErrors(ChatCompletionCreateResponse? response, string currentToken)
        {
            if (response == null)
            {
                tokenQueue.AddToken(currentToken);
                return;
            }

            if (response.Error?.Message != null && response.Error.Message.Contains("deactivated"))
            {
                openaiAccountsRepository.Remove(currentToken, DeletionReason.Deactivated);
            }
            else if (response.Error?.Code == "insufficient_quota")
            {
                openaiAccountsRepository.Remove(currentToken, DeletionReason.InsufficientQuota);
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
            }
            else
            {
                tokenQueue.AddToken(currentToken);
            }
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
        Task<ChatCompletionCreateResponse?> SendMessage(UpdateDecorator update, CancellationToken token);
    }
}
