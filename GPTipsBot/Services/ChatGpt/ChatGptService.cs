using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.Logging;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using Timer = System.Timers.Timer;
using GPTipsBot.Repositories;
using GPTipsBot.Models;
using Polly;
using GPTipsBot.Exceptions;
using System.Text.Json;

namespace GPTipsBot.Services
{
    public class ChatGptService : IGpt
    {
        private readonly ILogger<ChatGptService> log;
        private readonly OpenaiAccountsRepository openaiAccountsRepository;
        private readonly TokenQueue tokenQueue;
        private readonly OpenAiServiceCreator openAiServiceCreator;
        private readonly ContextWindow contextWindow;
        private Timer timer;

        public ChatGptService(ILogger<ChatGptService> log, OpenaiAccountsRepository openaiAccountsRepository,
            TokenQueue tokenQueue, OpenAiServiceCreator openAiServiceCreator, ContextWindow contextWindow)
        {
            this.log = log;
            this.openaiAccountsRepository = openaiAccountsRepository;
            this.tokenQueue = tokenQueue;
            this.openAiServiceCreator = openAiServiceCreator;
            this.contextWindow = contextWindow;
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
                textWithContext = contextWindow.GetContext(update.UserChatKey, update.Message.ContextId.Value);
            }

            return await SendMessageInternal(textWithContext, token);
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
                var currentToken = await openAiServiceCreator.GetApiKeyAsync();
                var openAiService = openAiServiceCreator.Create(currentToken);

                var retryAttempt = context.ContainsKey("retryAttempt")
                    ? (int)context["retryAttempt"] : 0;

                try
                {
                    response = await openAiService.ChatCompletion.CreateCompletion(
                        new ChatCompletionCreateRequest { Messages = messages }, cancellationToken: cancellationToken);

                    if (response.Successful)
                    {
                        openAiServiceCreator.ReturnApiKey(currentToken);
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    openAiServiceCreator.ReturnApiKey(currentToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (JsonException)
                {

                }
                catch (Exception ex)
                {
                    //just skip exceptions handled below
                }

                context["retryAttempt"] = retryAttempt + 1;

                HandleResponseErrors(response, currentToken);
                log.LogInformation("Failed request #{retryAttempt} to OpenAi service: [{Code}] {Message}",
                    retryAttempt, response?.Error?.Code, response?.Error?.Message);
                throw new ChatGptException(retryAttempt);
            }, new Context(), cancellationToken);

            return response;
        }

        private void HandleResponseErrors(ChatCompletionCreateResponse? response, string apiKey)
        {
            if (response == null)
            {
                openAiServiceCreator.ReturnApiKey(apiKey);
                return;
            }

            if (response.Error?.Message != null && response.Error.Message.Contains("deactivated"))
            {
                openaiAccountsRepository.RemoveApiKey(apiKey, DeletionReason.Deactivated);
            }
            else if (response.Error?.Code == "insufficient_quota")
            {
                openaiAccountsRepository.RemoveApiKey(apiKey, DeletionReason.InsufficientQuota);
            }
            else if (response.Error?.Code == "rate_limit_exceeded" && response.Error.Message != null)
            {
                if (response.Error.Message.Contains("on requests per day"))
                {
                    openaiAccountsRepository.FreezeApiKey(apiKey);
                }
                else if (response.Error.Message.Contains("on requests per min"))
                {
                    openAiServiceCreator.ReturnApiKey(apiKey);
                }
            }
            else
            {
                openAiServiceCreator.ReturnApiKey(apiKey);
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
