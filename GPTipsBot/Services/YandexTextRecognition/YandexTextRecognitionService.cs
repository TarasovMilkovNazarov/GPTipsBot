using System.Text.Json;
using System.Text.Json.Serialization;
using GPTipsBot.Models;
using GPTipsBot.Resources;
using GPTipsBot.Services.YandexTextRecognition;
using Microsoft.Extensions.Logging;
using HttpRequestMessage = System.Net.Http.HttpRequestMessage;

namespace GPTipsBot.Services
{
    public class YandexTextRecognitionService
    {
        private readonly ILogger<YandexTextRecognitionService> logger;
        private readonly HttpClient httpClient;
        private readonly string _token;
        private readonly string _folderId;

        public YandexTextRecognitionService(ILogger<YandexTextRecognitionService> logger, HttpClient httpClient)
        {
            this.logger = logger;
            this.httpClient = httpClient;
            _token = AppConfig.YandexCloudApiKey;
            _folderId = AppConfig.YandexCloudFolderId;
        }

        public async Task<string> Recognize(string base64String)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://ocr.api.cloud.yandex.net/ocr/v1/recognizeText");
            request.Headers.Add("Authorization", $"Api-Key {_token}");
            request.Headers.Add("x-folder-id", _folderId);
            request.Headers.Add("x-data-logging-enabled", "true");
            var body = new YandexRecognitionRequest()
            {
                Content = base64String
            };
            request.Content = new StringContent(JsonSerializer.Serialize(body), null, "application/json");

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var contentResult = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<Root>(contentResult);

            return !string.IsNullOrWhiteSpace(result?.result?.textAnnotation?.fullText) ? result.result.textAnnotation.fullText : BotResponse.CantRecognizeText;
        }
    }
}
