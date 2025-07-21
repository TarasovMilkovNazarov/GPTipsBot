using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GPTipsBot.Dtos;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.Logging;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using Timer = System.Timers.Timer;
using GPTipsBot.Repositories;
using GPTipsBot.Models;
using Polly;
using GPTipsBot.Exceptions;
using GPTipsBot.Resources;
using Polly.Retry;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot.Services
{
    public class ChatGptService : IGpt
    {
        private readonly ILogger<ChatGptService> _log;
        private readonly OpenaiAccountsRepository _openaiAccountsRepository;
        private readonly TokenQueue _tokenQueue;
        private readonly OpenAiServiceCreator _openAiServiceCreator;
        private readonly ContextWindow _contextWindow;
        private readonly ITelegramBotClient _botClient;
        private readonly HttpClient _httpClient;
        private Timer _timer;
        private readonly AsyncRetryPolicy _policy;

        private const int MaxRetryCount = 4;

        public ChatGptService(ILogger<ChatGptService> log, OpenaiAccountsRepository openaiAccountsRepository,
            TokenQueue tokenQueue, OpenAiServiceCreator openAiServiceCreator, ContextWindow contextWindow,
            ITelegramBotClient botClient, HttpClient httpClient)
        {
            _log = log;
            _openaiAccountsRepository = openaiAccountsRepository;
            _tokenQueue = tokenQueue;
            _openAiServiceCreator = openAiServiceCreator;
            _contextWindow = contextWindow;
            _botClient = botClient;
            _httpClient = httpClient;
            _timer = setup_Timer(openaiAccountsRepository);
            _policy = Policy
                .Handle<ChatGptException>()
                .WaitAndRetryAsync(MaxRetryCount, (retryAttempt) =>
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                    return delay;
                });
        }

        public async Task<ChatCompletionCreateResponse> SendMessage(UpdateDecorator update, CancellationToken token)
        {
            ChatMessage[] textWithContext;

            if (update.Message.NewContext)
            {
                textWithContext = new[] { new ChatMessage(update.Message.Role.ToString().ToLower(), update.Message.Text) };
            }
            else
            {
                textWithContext = _contextWindow.GetContext(update.UserChatKey, update.Message.ContextId.Value);
            }

            return await SendMessageInternal(textWithContext, token);
        }

        public async Task<Stream> GenerateMusicByText(string text, CancellationToken cancellationToken)
        {
            var currentToken = await _openAiServiceCreator.GetApiKeyAsync();
            var openAiService = _openAiServiceCreator.Create(currentToken);
            var audio = await openAiService.Audio.CreateSpeech<Stream>(new AudioCreateSpeechRequest
            {
                Model = "tta-stable/stable-audio",
                Input = text,
                Voice = "nova",
                ResponseFormat = "wav",
                Speed = 1.0f,
                ExtraBody = new ExtraBody
                {
                    SecondsTotal = 10
                }
            }, cancellationToken);

            return audio.Data;
        }

        public async Task<Uri> GenerateSongByText(string text, CancellationToken cancellationToken)
        {
            var model = "txt2sng-minimax/music";

            var jsonContent = JsonSerializer.Serialize(new GenerateRequest
            {
                Model = model,
                Action = "generate",
                Prompt = text
            });
            var generateResponse = await _httpClient.PostAsync("audio/generate",
                new StringContent(jsonContent, Encoding.UTF8), cancellationToken);

            var isGenerated = false;
            while (!isGenerated)
            {
                var response = await _httpClient.GetAsync("status?request_id={request_id}", cancellationToken);

                var generateData = await response.Content.ReadFromJsonAsync<GenerateResponse>
                    (cancellationToken: cancellationToken);

                if (generateData.Status == "COMPLETED")
                {
                    isGenerated = true;
                }
                else if (generateData.Status == "FAILED")
                {
                    throw new Exception("Failed to generate song");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }

            return _httpClient.BaseAddress;
        }

        private async Task<ChatCompletionCreateResponse?> SendMessageInternal(ChatMessage[] messages, CancellationToken cancellationToken)
        {
            _log.LogInformation("Send request to OpenAi service: {messages}", messages.Last().Content);

            ChatCompletionCreateResponse? response = null;

            await _policy.ExecuteAsync(async (context, _) =>
            {
                var currentToken = await _openAiServiceCreator.GetApiKeyAsync();
                var openAiService = _openAiServiceCreator.Create(currentToken);

                var retryAttempt = context.TryGetValue("retryAttempt", out var value)
                    ? (int)value : 0;

                try
                {
                    response = await openAiService.ChatCompletion.CreateCompletion(
                        new ChatCompletionCreateRequest { Messages = messages }, cancellationToken: cancellationToken);

                    if (response.Successful)
                    {
                        _openAiServiceCreator.ReturnApiKey(currentToken);
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    _openAiServiceCreator.ReturnApiKey(currentToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (Exception ex)
                {
                    //just skip exceptions handled below
                }

                context["retryAttempt"] = retryAttempt + 1;

                HandleResponseErrors(response, currentToken);
                _log.LogInformation("Failed request #{retryAttempt} to OpenAi service: [{Code}] {Message}",
                    retryAttempt, response?.Error?.Code, response?.Error?.Message);
                throw new ChatGptException(retryAttempt);
            }, new Context(), cancellationToken);

            return response;
        }

        private void HandleResponseErrors(ChatCompletionCreateResponse? response, string apiKey)
        {
            if (response == null)
            {
                _openAiServiceCreator.ReturnApiKey(apiKey);
                return;
            }

            if (response.Error?.Message != null && response.Error.Message.Contains("deactivated"))
            {
                _openaiAccountsRepository.RemoveApiKey(apiKey, DeletionReason.Deactivated);
            }
            else if (response.Error?.Code == "insufficient_quota")
            {
                _openaiAccountsRepository.RemoveApiKey(apiKey, DeletionReason.InsufficientQuota);
            }
            else if (response.Error?.Code == "rate_limit_exceeded" && response.Error.Message != null)
            {
                if (response.Error.Message.Contains("on requests per day"))
                {
                    _openaiAccountsRepository.FreezeApiKey(apiKey);
                }
                else if (response.Error.Message.Contains("on requests per min"))
                {
                    _openAiServiceCreator.ReturnApiKey(apiKey);
                }
            }
            else
            {
                _openAiServiceCreator.ReturnApiKey(apiKey);
            }
        }

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
        Task<ChatCompletionCreateResponse> SendMessage(UpdateDecorator update, CancellationToken token);
        Task<Stream> GenerateMusicByText(string text, CancellationToken cancellationToken);
        Task<Uri> GenerateSongByText(string text, CancellationToken cancellationToken);
    }
}
