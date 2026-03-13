using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ardalis.GuardClauses;
using GPTipsBot.Config;
using GPTipsBot.Dtos;
using Microsoft.Extensions.Logging;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using Timer = System.Timers.Timer;
using GPTipsBot.Repositories;
using GPTipsBot.Models;
using Polly;
using GPTipsBot.Exceptions;
using GPTipsBot.Extensions;
using GPTipsBot.Services.YandexPhotoAnimator;
using Polly.Retry;
using Telegram.Bot;
using GenerateRequest = GPTipsBot.Services.YandexPhotoAnimator.GenerateRequest;

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
        private readonly YaPhotoAnimatorService _yandexPhotoAnimator;
        private Timer _timer;
        private readonly AsyncRetryPolicy _policy;

        private const int MaxRetryCount = 4;

        public ChatGptService(ILogger<ChatGptService> log, OpenaiAccountsRepository openaiAccountsRepository,
            TokenQueue tokenQueue, OpenAiServiceCreator openAiServiceCreator, ContextWindow contextWindow,
            ITelegramBotClient botClient, HttpClient httpClient, YaPhotoAnimatorService yandexPhotoAnimator)
        {
            _log = log;
            _openaiAccountsRepository = openaiAccountsRepository;
            _tokenQueue = tokenQueue;
            _openAiServiceCreator = openAiServiceCreator;
            _contextWindow = contextWindow;
            _botClient = botClient;
            _httpClient = httpClient;
            _yandexPhotoAnimator = yandexPhotoAnimator;
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

        public async Task<Uri> GenerateVideoByText(string text, string? imageFileId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var model = imageFileId != null ? "img2vid-kling/standart16" : "txt2vid-kling/standart";
            var aspectRatio = "16:9";

            var generateRequestDto = new GPTipsBot.Dtos.GenerateRequest
            {
                Model = model,
                Action = "generate",
                Prompt = text,
                AspectRatio = aspectRatio,
            };
            if (imageFileId != null)
            {
                var base64Image = await imageFileId.GetPhotoAsync(_botClient);
                generateRequestDto.Image = string.Format(generateRequestDto.Image, base64Image);
            }

            var jsonContent = JsonSerializer.Serialize(generateRequestDto);

            var request = new HttpRequestMessage
            {
                Content = new StringContent(jsonContent, Encoding.UTF8),
                Method = HttpMethod.Post,
                RequestUri = new Uri(_httpClient.BaseAddress, "video/generate"),
                Headers =
                {
                    {"Accept", "application/json"},
                    {"Authorization", $"Bearer {AppConfig.ProxyApiApiKey}"},
                }
            };
            var generateResponse = await _httpClient.SendAsync(request, cancellationToken);
            var triggerGeenrationResult = await generateResponse.Content.ReadFromJsonAsync<GenerateResponse>
                (cancellationToken: cancellationToken);

            Guard.Against.Null(triggerGeenrationResult);
            Guard.Against.Null(triggerGeenrationResult.RequestId);

            var requestId = triggerGeenrationResult.RequestId;

            var isGenerated = false;
            string? url = null;

            while (!isGenerated)
            {
                var getStatusRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(_httpClient.BaseAddress, $"video/status?request_id={requestId}"))
                {
                    Headers =
                    {
                        {"Authorization", $"Key {AppConfig.ProxyApiApiKey}"},
                    }
                };
                var response = await _httpClient.SendAsync(getStatusRequest, cancellationToken);

                var generateResult = await response.Content.ReadFromJsonAsync<GenerateResponse>
                    (cancellationToken: cancellationToken);

                isGenerated = generateResult?.Status switch
                {
                    "COMPLETED" => true,
                    "FAILED" => throw new Exception("Failed to generate video"),
                    _ => isGenerated
                };

                if (isGenerated)
                {
                    url = generateResult?.Url;
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(120), cancellationToken);
            }

            Guard.Against.Null(url);

            return new Uri(url);
        }

        public async Task<Uri> AnimatePhoto(string prompt, string imageFileId, CancellationToken cancellationToken = default)
        {
            var base64Image = await imageFileId.GetPhotoAsync(_botClient);

            var imageUrl = await _yandexPhotoAnimator.UploadImageFromBase64(base64Image);
            var videoResponse = await _yandexPhotoAnimator.GenerateVideo(imageUrl, prompt);
            videoResponse = await _yandexPhotoAnimator.WaitForResult(videoResponse.VideoGeneration.Id);

            return new Uri(videoResponse.VideoGeneration.VideoURL);
        }

        public async Task<byte[]> CartoonifyImage(string imageFileId, CancellationToken cancellationToken = default)
        {
            var model = "img2img-aitransform/cartoonify";
            var aspectRatio = "16:9";

            var generateRequestDto = new GPTipsBot.Dtos.GenerateRequest
            {
                Model = model,
                Action = "generate",
                AspectRatio = aspectRatio,
                Prompt = "Frozen"
            };

            var base64Image = await imageFileId.GetPhotoAsync(_botClient);
            generateRequestDto.Image = string.Format(generateRequestDto.Image, base64Image);

            var jsonContent = JsonSerializer.Serialize(generateRequestDto);

            var request = new HttpRequestMessage
            {
                Content = new StringContent(jsonContent, Encoding.UTF8),
                Method = HttpMethod.Post,
                RequestUri = new Uri(_httpClient.BaseAddress, "images/generations"),
                Headers =
                {
                    {"Accept", "application/json"},
                    {"Authorization", $"Bearer {AppConfig.ProxyApiApiKey}"},
                }
            };
            var generateResponse = await _httpClient.SendAsync(request, cancellationToken);
            var generateResult = await generateResponse.Content.ReadFromJsonAsync<GenerateResponse>
                (cancellationToken: cancellationToken);

            return Convert.FromBase64String(generateResult.Data[0].Base64Json);
        }

        public async Task<Uri> GenerateSongByText(string text, CancellationToken cancellationToken)
        {
            var model = "txt2sng-minimax/music";

            var jsonContent = JsonSerializer.Serialize(new GPTipsBot.Dtos.GenerateRequest
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
        Task<Uri> GenerateVideoByText(string text, string? imageFileId, CancellationToken cancellationToken);
        Task<Uri> GenerateSongByText(string text, CancellationToken cancellationToken);
        Task<byte[]> CartoonifyImage(string imageFileId, CancellationToken cancellationToken = default);
        Task<Uri> AnimatePhoto(string prompt, string imageFileId, CancellationToken cancellationToken = default);
    }
}
