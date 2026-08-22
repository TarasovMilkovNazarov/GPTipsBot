using System.Net.Http.Headers;
using System.Text.Json;
using GPTipsBot.Config;
using GPTipsBot.Dtos;
using GPTipsBot.Resources;
using Microsoft.Extensions.Logging;
using HttpRequestMessage = System.Net.Http.HttpRequestMessage;

namespace GPTipsBot.Services.YandexCloud
{
    public interface ITextRecognizer
    {
        Task<string> Recognize(string base64String);
    }

    public record ImageGenerationStatus(bool Done, string? ImageBase64);

    public interface IImageGenerator
    {
        Task<string> StartImageGenerationAsync(string prompt, bool square);
        Task<ImageGenerationStatus> GetImageGenerationStatusAsync(string operationId);
    }

    public class YaCloudClient : ITextRecognizer, IImageGenerator
    {
        private readonly ILogger<YaCloudClient> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _folderId;

        public YaCloudClient(ILogger<YaCloudClient> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Api-Key", AppConfig.YandexCloudApiKey);
            _folderId = AppConfig.YandexCloudFolderId;
        }

        public async Task<string> Recognize(string base64String)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://ocr.api.cloud.yandex.net/ocr/v1/recognizeText");
            request.Headers.Add("x-folder-id", _folderId);
            request.Headers.Add("x-data-logging-enabled", "true");
            var body = new YandexRecognitionRequest()
            {
                Content = base64String
            };
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            request.Content = new StringContent(JsonSerializer.Serialize(body, options), null, "application/json");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var contentResult = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<Root>(contentResult, options);

            return !string.IsNullOrWhiteSpace(result?.Result?.TextAnnotation?.FullText) ? result.Result.TextAnnotation.FullText : BotResponse.CantRecognizeText;
        }

        public async Task<string> StartImageGenerationAsync(string prompt, bool square)
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://llm.api.cloud.yandex.net/foundationModels/v1/imageGenerationAsync");

            var aspectRatio = new AspectRatio
            {
                WidthRatio = square ? "1" : "2",
                HeightRatio = "1"
            };
            var body = new YandexArtRequest
            {
                ModelUri = $"art://{_folderId}/yandex-art/latest",
                GenerationOptions = new GenerationOptions
                {
                    Seed = "1863",
                    AspectRatio = aspectRatio
                },
                Messages = new List<Message>()
                {
                    new()
                    {
                        Weight = "1",
                        Text = prompt
                    }
                }
            };
            request.Content = new StringContent(JsonSerializer.Serialize(body), null, "application/json");

            var response = await _httpClient.SendAsync(request);
            var contentResult = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                if (YandexArtErrors.IsContentRejection(response.StatusCode, contentResult))
                {
                    YandexArtErrors.TryParse(contentResult, out var rejection);
                    _logger.LogWarning(
                        "YandexART rejected prompt (code {Code}). Prompt length: {PromptLength}. Response: {ResponseBody}",
                        rejection?.Code,
                        prompt.Length,
                        contentResult);
                    throw new YandexArtRejectedException(
                        rejection?.Code,
                        rejection?.Message ?? rejection?.Error);
                }

                _logger.LogError(
                    "YandexART imageGenerationAsync failed with {StatusCode}. Prompt length: {PromptLength}. Response: {ResponseBody}",
                    (int)response.StatusCode,
                    prompt.Length,
                    contentResult);
                throw new HttpRequestException(
                    $"YandexART imageGenerationAsync failed with {(int)response.StatusCode} ({response.ReasonPhrase}): {contentResult}",
                    null,
                    response.StatusCode);
            }

            var result = JsonSerializer.Deserialize<YandexArtResponse>(contentResult);
            return result?.Id ?? throw new Exception("YandexART did not return operation id");
        }

        public async Task<ImageGenerationStatus> GetImageGenerationStatusAsync(string operationId)
        {
            var getResultResponse = await _httpClient.GetAsync(
                $"https://llm.api.cloud.yandex.net:443/operations/{operationId}");
            var contentResult = await getResultResponse.Content.ReadAsStringAsync();
            if (!getResultResponse.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "YandexART get operation {OperationId} failed with {StatusCode}. Response: {ResponseBody}",
                    operationId,
                    (int)getResultResponse.StatusCode,
                    contentResult);
                throw new HttpRequestException(
                    $"YandexART get operation failed with {(int)getResultResponse.StatusCode} ({getResultResponse.ReasonPhrase}): {contentResult}",
                    null,
                    getResultResponse.StatusCode);
            }

            var result = JsonSerializer.Deserialize<YandexArtResponse>(contentResult);

            return new ImageGenerationStatus(result?.Done == true, result?.Response?.Image);
        }
    }
}
