using System.ClientModel;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;
using GPTipsBot.Repositories;
using GPTipsBot.Models;
using Polly;
using GPTipsBot.Exceptions;
using Polly.Retry;
using OpenAI;
using OpenAI.Audio;
using OpenAI.Chat;

namespace GPTipsBot.Services
{
    public class ChatGptService : IGpt
    {
        private readonly ILogger<ChatGptService> _log;
        private readonly OpenaiAccountsRepository _openaiAccountsRepository;
        private readonly TokenQueue _tokenQueue;
        private readonly OpenAiServiceCreator _openAiServiceCreator;
        private readonly ContextWindow _contextWindow;
        private Timer _timer;
        private readonly AsyncRetryPolicy _policy;
        private readonly OpenAIClient _openAiClient;

        private const int MaxRetryCount = 4;

        public ChatGptService(ILogger<ChatGptService> log, OpenaiAccountsRepository openaiAccountsRepository,
            TokenQueue tokenQueue, OpenAiServiceCreator openAiServiceCreator, ContextWindow contextWindow)
        {
            _log = log;
            _openaiAccountsRepository = openaiAccountsRepository;
            _tokenQueue = tokenQueue;
            _openAiServiceCreator = openAiServiceCreator;
            _contextWindow = contextWindow;
            _timer = setup_Timer(openaiAccountsRepository);
            _policy = Policy
                .Handle<ChatGptException>()
                .WaitAndRetryAsync(MaxRetryCount, (retryAttempt) =>
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                    return delay;
                });

            _openAiClient = _openAiServiceCreator.Create(AppConfig.ProxyApiApiKey);
        }

        public async Task GenerateAudio(string text, CancellationToken cancellationToken)
        {
            var audioClient = _openAiClient.GetAudioClient("tts-1");

            var result = await audioClient.GenerateSpeechAsync(text, GeneratedSpeechVoice.Nova,
                new SpeechGenerationOptions
                {
                    SpeedRatio = 1.0f,
                    ResponseFormat = GeneratedSpeechFormat.Wav
                },
                cancellationToken: cancellationToken);
        }

        public async Task<ClientResult<ChatCompletion>?> SendMessage(UpdateDecorator update, CancellationToken token)
        {
            ChatMessage[] textWithContext;

            if (update.Message.NewContext)
            {
                textWithContext = new ChatMessage[] { new UserChatMessage(update.Message.Text) };
            }
            else
            {
                textWithContext = _contextWindow.GetContext(update.UserChatKey, update.Message.ContextId);
            }

            return await SendMessageInternal(textWithContext, token);
        }

        private async Task<ClientResult<ChatCompletion>?> SendMessageInternal(ChatMessage[] messages, CancellationToken cancellationToken)
        {
            _log.LogInformation("Send request to OpenAi service: {messages}", messages.Last().Content);

            ClientResult<ChatCompletion>? response = null;

            await _policy.ExecuteAsync(async (context, _) =>
            {
                var retryAttempt = context.TryGetValue("retryAttempt", out var value)
                    ? (int)value : 0;

                try
                {
                    response = await _openAiClient.GetChatClient("openai/gpt-3.5-turbo")
                        .CompleteChatAsync(messages, cancellationToken: cancellationToken);

                    if (response.Value.Content != null)
                    {
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (Exception ex)
                {
                    //just skip exceptions handled below
                }

                context["retryAttempt"] = retryAttempt + 1;

                // _log.LogInformation("Failed request #{retryAttempt} to OpenAi service: [{Code}] {Message}",
                //     retryAttempt, response?.Error?.Code, response?.Error?.Message);
                throw new ChatGptException(retryAttempt);
            }, new Context(), cancellationToken);

            return response;
        }

        // private void HandleResponseErrors(ClientResult<ChatCompletion>? response, string apiKey)
        // {
        //     if (response == null)
        //     {
        //         _openAiServiceCreator.ReturnApiKey(apiKey);
        //         return;
        //     }
        //
        //     if (response.Error?.Message != null && response.Error.Message.Contains("deactivated"))
        //     {
        //         _openaiAccountsRepository.RemoveApiKey(apiKey, DeletionReason.Deactivated);
        //     }
        //     else if (response.Error?.Code == "insufficient_quota")
        //     {
        //         _openaiAccountsRepository.RemoveApiKey(apiKey, DeletionReason.InsufficientQuota);
        //     }
        //     else if (response.Error?.Code == "rate_limit_exceeded" && response.Error.Message != null)
        //     {
        //         if (response.Error.Message.Contains("on requests per day"))
        //         {
        //             _openaiAccountsRepository.FreezeApiKey(apiKey);
        //         }
        //         else if (response.Error.Message.Contains("on requests per min"))
        //         {
        //             _openAiServiceCreator.ReturnApiKey(apiKey);
        //         }
        //     }
        //     else
        //     {
        //         _openAiServiceCreator.ReturnApiKey(apiKey);
        //     }
        // }

        private Timer setup_Timer(OpenaiAccountsRepository openaiAccountsRepository)
        {
            var delay = TimeSpan.FromMinutes(1).TotalMinutes;

            var nowTime = DateTime.Now;
            var specificTime = nowTime.Date.AddDays(1).AddMinutes(delay);
            if (nowTime > specificTime)
                specificTime = specificTime.AddDays(1);

            var tickTime = (specificTime - nowTime).TotalMilliseconds;
            _timer = new Timer(tickTime);
            _timer.Elapsed += (s, e) => UnfreezeDayLimitedTokens(openaiAccountsRepository);
            _timer.Start();

            return _timer;
        }

        private void UnfreezeDayLimitedTokens(OpenaiAccountsRepository openaiAccountsRepository)
        {
            _timer.Stop();

            var unfreezed = openaiAccountsRepository.UnfreezeTokens();
            foreach (var item in unfreezed)
            {
                _tokenQueue.AddToken(item);
            }

            _timer = setup_Timer(openaiAccountsRepository);
        }
    }

    public interface IGpt
    {
        Task<ClientResult<ChatCompletion>?> SendMessage(UpdateDecorator update, CancellationToken token);
    }
}
