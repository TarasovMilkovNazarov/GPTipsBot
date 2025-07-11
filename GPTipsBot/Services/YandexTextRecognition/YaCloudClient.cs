using System.Net.Http.Headers;
using System.Text.Json;
using GPTipsBot.Dtos;
using GPTipsBot.Models;
using GPTipsBot.Resources;
using GPTipsBot.Services.YandexTextRecognition;
using Microsoft.Extensions.Logging;
using HttpRequestMessage = System.Net.Http.HttpRequestMessage;
using Message = GPTipsBot.Services.YandexTextRecognition.Message;

namespace GPTipsBot.Services
{
    public interface ITextRecognizer
    {
        Task<string> Recognize(string base64String);
    }

    public interface IImageGenerator
    {
        Task<string> GenerateImage(string prompt);
    }

    public class YaCloudClient : ITextRecognizer, IImageGenerator
    {
        private readonly ILogger<YaCloudClient> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _token;
        private readonly string _folderId;

        public YaCloudClient(ILogger<YaCloudClient> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            _token = AppConfig.YandexCloudApiKey;
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
            request.Content = new StringContent(JsonSerializer.Serialize(body), null, "application/json");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var contentResult = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<Root>(contentResult);

            return !string.IsNullOrWhiteSpace(result?.Result?.TextAnnotation?.FullText) ? result.Result.TextAnnotation.FullText : BotResponse.CantRecognizeText;
        }

        public async Task<string> GenerateImage(string prompt)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://llm.api.cloud.yandex.net/foundationModels/v1/imageGenerationAsync");
            var body = new YandexArtRequest
            {
                ModelUri = $"art://{_folderId}/yandex-art/latest",
                GenerationOptions = new GenerationOptions
                {
                    Seed = "1863",
                    AspectRatio = new AspectRatio
                    {
                        WidthRatio = "2",
                        HeightRatio = "1"
                    }
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
            response.EnsureSuccessStatusCode();
            var contentResult = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<YandexArtResponse>(contentResult);

            while (true)
            {
                await Task.Delay(3000);
                var getResultResponse = await _httpClient.GetAsync($"https://llm.api.cloud.yandex.net:443/operations/{result.Id}");

                result = JsonSerializer.Deserialize<YandexArtResponse>(await getResultResponse.Content.ReadAsStringAsync());

                if (result?.Done == true)
                {
                    break;
                }
            }

            return result?.Response?.Image ?? throw new Exception();
        }
    }
}
