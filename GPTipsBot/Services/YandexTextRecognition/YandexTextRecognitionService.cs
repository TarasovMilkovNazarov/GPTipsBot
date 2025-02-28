using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using GPTipsBot.Models;
using GPTipsBot.Resources;
using GPTipsBot.Services.YandexTextRecognition;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using HttpRequestMessage = System.Net.Http.HttpRequestMessage;
using Message = GPTipsBot.Services.YandexTextRecognition.Message;

namespace GPTipsBot.Services
{
    public class YandexTextRecognitionService
    {
        private readonly ILogger<YandexTextRecognitionService> logger;
        private readonly HttpClient _httpClient;
        private readonly string _token;
        private readonly string _folderId;

        public YandexTextRecognitionService(ILogger<YandexTextRecognitionService> logger, HttpClient httpClient)
        {
            this.logger = logger;
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

            return !string.IsNullOrWhiteSpace(result?.result?.textAnnotation?.fullText) ? result.result.textAnnotation.fullText : BotResponse.CantRecognizeText;
        }

        public async Task<string> GenerateImage(string prompt)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://llm.api.cloud.yandex.net/foundationModels/v1/imageGenerationAsync");
            var body = new YandexArtRequest
            {
                modelUri = $"art://{_folderId}/yandex-art/latest",
                generationOptions = new GenerationOptions
                {
                    seed = "1863",
                    aspectRatio = new AspectRatio
                    {
                        widthRatio = "2",
                        heightRatio = "1"
                    }
                },
                messages = new List<Message>()
                {
                    new()
                    {
                        weight = "1",
                        text = prompt
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
                var getResultResponse = await _httpClient.GetAsync($"https://llm.api.cloud.yandex.net:443/operations/{result.id}");

                result = JsonSerializer.Deserialize<YandexArtResponse>(await getResultResponse.Content.ReadAsStringAsync());

                if (result?.done == true)
                {
                    break;
                }
            }

            return result?.response?.image ?? throw new Exception();
        }
    }
}
