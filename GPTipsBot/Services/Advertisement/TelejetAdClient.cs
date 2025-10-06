using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GPTipsBot.Config;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace GPTipsBot.Services
{
    public class TelejetAdClient(ILogger<TelejetAdClient> logger)
    {
        private readonly string _apiKey = AppConfig.TelejetApiKey;
        private const string BapPrefix = "/__bap";
        private const int ApiVersion = 3;
        private const string Host = "bap.teleads.pro";
        private const int Port = 8080;

        private readonly UdpClient _udpClient = new(Host, Port);

        /// <summary>
        /// If this method returns true then update should be processed by bot otherwise ignore it
        /// </summary>
        /// <param name="update"></param>
        /// <returns></returns>
        public async Task<bool> HandleUpdateAsync(Update update)
        {
            return !IsBapUpdate(update);
        }

        public async Task SendToBapAsync(Update update, string method)
        {
            return;

            try
            {
                var dto = new TelejetDto
                {
                    ApiKey = _apiKey,
                    Version = ApiVersion,
                    Update = update,
                    Method = method
                };

                var json = Serialize(dto);
                var data = Encoding.UTF8.GetBytes(json);
                await _udpClient.Client.ConnectAsync(Host, Port);
                await _udpClient.SendAsync(data, data.Length);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send data to BAP API");
            }
        }

        private string Serialize(TelejetDto dto)
        {
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
            });

            return json;
        }

        private bool IsBapUpdate(Update update)
        {
            var data = update.CallbackQuery?.Data;

            return data != null && data.StartsWith(BapPrefix);
        }
    }

    class TelejetDto
    {
        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; }
        [JsonPropertyName("method")]
        public string Method { get; set; }
        [JsonPropertyName("version")]
        public int Version { get; set; }
        [JsonPropertyName("update")]
        public Update Update { get; set; }
    }
}
