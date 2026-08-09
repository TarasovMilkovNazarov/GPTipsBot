using RestSharp;
using System.Net;
using GPTipsBot.Config;
using Telegram.Bot;

namespace GPTipsBot.Services
{
    public class SpeechToTextService(ITelegramBotClient telegramBotClient)
    {
        public async Task<string> RecognizeVoice(string fileId)
        {
            using var stream = new MemoryStream();
            var file = await telegramBotClient.GetInfoAndDownloadFile(fileId, stream);
            if (file == null)
            {
                throw new Exception("Can't download file from telegram. The file size should be less than 20mb");
            }

            return await RecognizeAudioAsync(stream.ToArray());
        }

        public async Task<string> RecognizeAudioAsync(byte[] audioBytes, CancellationToken cancellationToken = default)
        {
            if (audioBytes.Length == 0)
            {
                throw new Exception("Empty audio");
            }

            if (audioBytes.Length > 20 * 1024 * 1024)
            {
                throw new Exception("Audio must be less than 20mb");
            }

            var lang = "auto";
            var client = new RestClient("https://stt.api.cloud.yandex.net");
            var request = new RestRequest($"speech/v1/stt:recognize?topic=general&lang={lang}&" +
                                          $"folderId={AppConfig.YandexCloudFolderId}", Method.Post);
            request.AddHeader("Authorization", $"Api-Key {AppConfig.YandexCloudApiKey}");
            request.AddHeader("Content-Type", "application/octet-stream");
            request.AddParameter("application/octet-stream", audioBytes, ParameterType.RequestBody);

            var response = await client.ExecuteAsync<RecognitionResult>(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.OK && response.Data != null)
            {
                return response.Data.Result;
            }

            throw new Exception("Request failed: " + (response.ErrorMessage ?? response.StatusCode.ToString()));
        }
    }

    class RecognitionResult
    {
        public string Result { get; set; }
    }
}
