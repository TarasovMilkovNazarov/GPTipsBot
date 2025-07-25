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

            var lang = "auto";
            var client = new RestClient("https://stt.api.cloud.yandex.net");
            var request = new RestRequest($"speech/v1/stt:recognize?topic=general&lang={lang}&" +
                                          $"folderId={AppConfig.YandexCloudFolderId}", Method.Post);
            request.AddHeader("Authorization", $"Api-Key {AppConfig.YandexCloudApiKey}");
            request.AddHeader("Content-Type", "application/octet-stream");
            request.AddParameter("application/octet-stream", stream.ToArray(), ParameterType.RequestBody);
            var cancellationTokenSource = new CancellationTokenSource();

            var response = await client.ExecuteAsync<RecognitionResult>(request, cancellationTokenSource.Token);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return response.Data.Result;
            }

            throw new Exception("Request failed: " + response.ErrorMessage);
        }
    }

    class RecognitionResult
    {
        public string Result { get; set; }
    }
}
